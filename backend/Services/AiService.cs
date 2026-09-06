using Dapper;
using FinApp.Api.Data;
using FinApp.Api.Models;
using System.Text.Json;

namespace FinApp.Api.Services;

public class AiService(DbConnectionFactory db, FinancialContextPlugin contextPlugin)
{
    // ── Configuração do provedor ───────────────────────────────────────────
    // AI_PROVIDER=groq  → usa Groq (padrão)
    // AI_PROVIDER=ollama → usa Ollama local
    private readonly string _provider =
        Environment.GetEnvironmentVariable("AI_PROVIDER") ?? "groq";

    private readonly string _groqApiKey =
        Environment.GetEnvironmentVariable("GROQ_API_KEY") ?? "";

    private readonly string _groqModel =
        Environment.GetEnvironmentVariable("GROQ_MODEL") ?? "openai/gpt-oss-20b";

    private readonly string _ollamaUrl =
        Environment.GetEnvironmentVariable("OLLAMA_URL") ?? "http://localhost:11434";

    private readonly string _ollamaModel =
        Environment.GetEnvironmentVariable("OLLAMA_MODEL") ?? "phi3";

    // Opções de serialização JSON
    private static readonly JsonSerializerOptions SerializeOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
    private static readonly JsonSerializerOptions DeserializeOpts = new()
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
        var rows = await conn.ExecuteAsync(
            "DELETE FROM ai_chats WHERE id = @Id AND user_id = @UserId",
            new { Id = chatId, UserId = userId });
        return rows > 0;
    }

    // ── Messages ───────────────────────────────────────────────────────────

    public async Task<IEnumerable<AiMessage>> GetMessagesAsync(int chatId, int userId)
    {
        using var conn = db.Create();

        var owns = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM ai_chats WHERE id = @Id AND user_id = @UserId",
            new { Id = chatId, UserId = userId });
        if (owns == 0) return [];

        return await conn.QueryAsync<AiMessage>(@"
            SELECT id, chat_id, role, content, created_at
            FROM ai_messages
            WHERE chat_id = @ChatId AND role != 'system'
            ORDER BY created_at ASC",
            new { ChatId = chatId });
    }

    public async Task<AiMessage> SendMessageAsync(
        int chatId, int userId, string userName, string userContent)
    {
        using var conn = db.Create();

        var chat = await conn.QueryFirstOrDefaultAsync<AiChat>(
            "SELECT * FROM ai_chats WHERE id = @Id AND user_id = @UserId",
            new { Id = chatId, UserId = userId });
        if (chat is null) throw new UnauthorizedAccessException("Chat não encontrado.");

        // 1. Monta o system prompt com dados financeiros frescos
        var financialContext = await contextPlugin.BuildContextAsync(userId, userName);
        var systemPrompt = FinancialContextPlugin.BuildSystemPrompt(financialContext);

        // 2. Carrega histórico
        var history = (await conn.QueryAsync<AiMessage>(@"
            SELECT role, content FROM ai_messages
            WHERE chat_id = @ChatId AND role != 'system'
            ORDER BY created_at ASC",
            new { ChatId = chatId })).ToList();

        // 3. Salva mensagem do usuário
        await conn.ExecuteAsync(@"
            INSERT INTO ai_messages (chat_id, role, content)
            VALUES (@ChatId, 'user', @Content)",
            new { ChatId = chatId, Content = userContent });

        // 4. Monta lista de mensagens
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

        // 5. Chama o provedor configurado
        string assistantContent = _provider.ToLower() == "ollama"
            ? await CallOllamaAsync(messages)
            : await CallGroqAsync(messages);

        // 6. Salva resposta do assistente
        var assistantMsgId = await conn.ExecuteScalarAsync<int>(@"
            INSERT INTO ai_messages (chat_id, role, content)
            VALUES (@ChatId, 'assistant', @Content);
            SELECT LAST_INSERT_ID();",
            new { ChatId = chatId, Content = assistantContent });

        // 7. Atualiza título do chat
        if (chat.Title == "Nova conversa")
        {
            var autoTitle = userContent.Length > 50
                ? userContent[..47] + "..."
                : userContent;
            await conn.ExecuteAsync(
                "UPDATE ai_chats SET title = @Title, updated_at = NOW() WHERE id = @Id",
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

    // ── Groq ───────────────────────────────────────────────────────────────

    private async Task<string> CallGroqAsync(List<OllamaMessage> messages)
    {
        try
        {
            if (string.IsNullOrEmpty(_groqApiKey))
                return "Erro: GROQ_API_KEY não configurada no .env.";

            var request = new OllamaRequest
            {
                Model = _groqModel,
                Messages = messages,
                Stream = false,
            };

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            http.DefaultRequestHeaders.Add("Authorization", $"Bearer {_groqApiKey}");

            var json = JsonSerializer.Serialize(request, SerializeOpts);
            var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var response = await http.PostAsync("https://api.groq.com/openai/v1/chat/completions", httpContent);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return $"Erro Groq {(int)response.StatusCode}: {body}";

            // Groq usa formato OpenAI: choices[0].message.content
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement
                       .GetProperty("choices")[0]
                       .GetProperty("message")
                       .GetProperty("content")
                       .GetString()
                   ?? "Sem resposta.";
        }
        catch (Exception ex)
        {
            return $"Erro ao conectar com Groq: {ex.Message}";
        }
    }

    // ── Ollama (fallback) ──────────────────────────────────────────────────

    private async Task<string> CallOllamaAsync(List<OllamaMessage> messages)
    {
        try
        {
            var request = new OllamaRequest
            {
                Model = _ollamaModel,
                Messages = messages,
                Stream = false,
            };

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
            var json = JsonSerializer.Serialize(request, SerializeOpts);
            var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var response = await http.PostAsync($"{_ollamaUrl}/api/chat", httpContent);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return $"Erro Ollama {(int)response.StatusCode}: {body}";

            var ollamaResponse = JsonSerializer.Deserialize<OllamaResponse>(body, DeserializeOpts);
            return ollamaResponse?.Message?.Content ?? "Sem resposta.";
        }
        catch (Exception ex)
        {
            return $"Erro ao conectar com Ollama: {ex.Message}";
        }
    }
}