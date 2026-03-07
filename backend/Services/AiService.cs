using Dapper;
using FinApp.Api.Data;
using FinApp.Api.Models;
using System.Text.Json;

namespace FinApp.Api.Services;

public class AiService(DbConnectionFactory db, FinancialContextPlugin contextPlugin)
{
    private readonly string _ollamaUrl =
        Environment.GetEnvironmentVariable("OLLAMA_URL") ?? "http://localhost:11434";

    private readonly string _ollamaModel =
        Environment.GetEnvironmentVariable("OLLAMA_MODEL") ?? "phi3";

    // Opções para serializar o REQUEST ao Ollama — camelCase simples
    private static readonly JsonSerializerOptions OllamaSerializeOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    // Opções para deserializar a RESPONSE do Ollama — case-insensitive
    private static readonly JsonSerializerOptions OllamaDeserializeOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    // ── Chats ──────────────────────────────────────────────────────────────

    public async Task<IEnumerable<AiChatSummary>> ListChatsAsync(int userId)
    {
        using var conn = db.Create();
        return await conn.QueryAsync<AiChatSummary>(@"
            SELECT id, title, updated_at
            FROM ai_chats
            WHERE user_id = @UserId
            ORDER BY updated_at DESC",
            new { UserId = userId });
    }

    public async Task<AiChat> CreateChatAsync(int userId, string? title)
    {
        using var conn = db.Create();
        var id = await conn.ExecuteScalarAsync<int>(@"
            INSERT INTO ai_chats (user_id, title)
            VALUES (@UserId, @Title);
            SELECT LAST_INSERT_ID();",
            new { UserId = userId, Title = title ?? "Nova conversa" });

        return new AiChat { Id = id, UserId = userId, Title = title ?? "Nova conversa" };
    }

    public async Task<bool> DeleteChatAsync(int chatId, int userId)
    {
        using var conn = db.Create();
        var rows = await conn.ExecuteAsync(@"
            DELETE FROM ai_chats WHERE id = @Id AND user_id = @UserId",
            new { Id = chatId, UserId = userId });
        return rows > 0;
    }

    // ── Messages ───────────────────────────────────────────────────────────

    public async Task<IEnumerable<AiMessage>> GetMessagesAsync(int chatId, int userId)
    {
        using var conn = db.Create();

        // Verifica que o chat pertence ao usuário
        var owns = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM ai_chats WHERE id = @Id AND user_id = @UserId",
            new { Id = chatId, UserId = userId });
        if (owns == 0) return [];

        return await conn.QueryAsync<AiMessage>(@"
            SELECT id, chat_id, role, content, created_at
            FROM ai_messages
            WHERE chat_id = @ChatId
              AND role != 'system'
            ORDER BY created_at ASC",
            new { ChatId = chatId });
    }

    /// <summary>
    /// Recebe a mensagem do usuário, chama o Ollama com contexto financeiro completo
    /// e persiste tanto a mensagem do usuário quanto a resposta do assistente.
    /// </summary>
    public async Task<AiMessage> SendMessageAsync(
        int chatId, int userId, string userName, string userContent)
    {
        using var conn = db.Create();

        // Garante que o chat pertence ao usuário
        var chat = await conn.QueryFirstOrDefaultAsync<AiChat>(
            "SELECT * FROM ai_chats WHERE id = @Id AND user_id = @UserId",
            new { Id = chatId, UserId = userId });
        if (chat is null) throw new UnauthorizedAccessException("Chat não encontrado.");

        // 1. Monta o system prompt com dados financeiros frescos
        var financialContext = await contextPlugin.BuildContextAsync(userId, userName);
        var systemPrompt = FinancialContextPlugin.BuildSystemPrompt(financialContext);

        // 2. Carrega histórico de mensagens da conversa (sem o system)
        var history = (await conn.QueryAsync<AiMessage>(@"
            SELECT role, content FROM ai_messages
            WHERE chat_id = @ChatId AND role != 'system'
            ORDER BY created_at ASC",
            new { ChatId = chatId })).ToList();

        // 3. Salva a mensagem do usuário
        await conn.ExecuteAsync(@"
            INSERT INTO ai_messages (chat_id, role, content)
            VALUES (@ChatId, 'user', @Content)",
            new { ChatId = chatId, Content = userContent });

        // 4. Monta o payload para o Ollama
        var messages = new List<OllamaMessage>
        {
            new() { Role = "system", Content = systemPrompt }
        };
        messages.AddRange(history.Select(h => new OllamaMessage
        {
            Role = h.Role,
            Content = h.Content,
        }));
        messages.Add(new OllamaMessage { Role = "user", Content = userContent });

        var ollamaRequest = new OllamaRequest
        {
            Model = _ollamaModel,
            Messages = messages,
            Stream = false,
        };

        // 5. Chama o Ollama
        string assistantContent;
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
            var json = JsonSerializer.Serialize(ollamaRequest, OllamaSerializeOpts);
            var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await http.PostAsync($"{_ollamaUrl}/api/chat", httpContent);

            // Lê o body mesmo em caso de erro para expor a mensagem real do Ollama
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Ollama retornou {(int)response.StatusCode}: {responseBody}");

            var ollamaResponse = JsonSerializer.Deserialize<OllamaResponse>(responseBody, OllamaDeserializeOpts);
            assistantContent = ollamaResponse?.Message?.Content
                ?? "Desculpe, não consegui gerar uma resposta.";
        }
        catch (Exception ex)
        {
            assistantContent = $"Erro ao conectar com o assistente: {ex.Message}";
        }

        // 6. Salva a resposta do assistente
        var assistantMsgId = await conn.ExecuteScalarAsync<int>(@"
            INSERT INTO ai_messages (chat_id, role, content)
            VALUES (@ChatId, 'assistant', @Content);
            SELECT LAST_INSERT_ID();",
            new { ChatId = chatId, Content = assistantContent });

        // 7. Atualiza o título do chat se ainda for "Nova conversa"
        if (chat.Title == "Nova conversa")
        {
            var autoTitle = userContent.Length > 50
                ? userContent[..47] + "..."
                : userContent;
            await conn.ExecuteAsync(@"
                UPDATE ai_chats SET title = @Title, updated_at = NOW()
                WHERE id = @Id",
                new { Title = autoTitle, Id = chatId });
        }
        else
        {
            await conn.ExecuteAsync(
                "UPDATE ai_chats SET updated_at = NOW() WHERE id = @Id",
                new { Id = chatId });
        }

        return new AiMessage
        {
            Id = assistantMsgId,
            ChatId = chatId,
            Role = "assistant",
            Content = assistantContent,
        };
    }
}