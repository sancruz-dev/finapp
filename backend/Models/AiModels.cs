namespace FinApp.Api.Models;

// ── Entidades do banco ─────────────────────────────────────────────────────

public class AiChat
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Title { get; set; } = "Nova conversa";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<AiMessage> Messages { get; set; } = [];
}

public class AiMessage
{
    public int Id { get; set; }
    public int ChatId { get; set; }
    public string Role { get; set; } = "user";   // "user" | "assistant" | "system"
    public string Content { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

// ── DTOs ───────────────────────────────────────────────────────────────────

public record CreateAiChatRequest(string? Title);

public record SendAiMessageRequest(string Content);

public record AiChatSummary(int Id, string Title, DateTime UpdatedAt);

// ── Ollama API contracts ───────────────────────────────────────────────────

public class OllamaRequest
{
    public string Model { get; set; } = "";
    public List<OllamaMessage> Messages { get; set; } = [];
    public bool Stream { get; set; } = false;
}

public class OllamaMessage
{
    public string Role { get; set; } = "";
    public string Content { get; set; } = "";
}

public class OllamaResponse
{
    public OllamaMessage? Message { get; set; }
}

// ── Contexto financeiro (montado pelo plugin) ──────────────────────────────

public class FinancialContext
{
    public string UserName { get; set; } = "";
    public MonthSummary CurrentMonth { get; set; } = new();
    public List<MonthSummary> Last12Months { get; set; } = [];
    public List<CategoryInfo> Categories { get; set; } = [];
    public List<MerchantMonthSpending> MerchantSpending { get; set; } = [];
}

public class MerchantMonthSpending
{
    public string  MerchantName  { get; set; } = "";
    public int     Year          { get; set; }
    public int     Month         { get; set; }
    public int     PurchaseCount { get; set; }
    public decimal TotalSpent    { get; set; }
}

public class MonthSummary
{
    public string Month { get; set; } = "";        // "2026-02"
    public string MonthLabel { get; set; } = "";   // "Fevereiro/2026"
    public decimal TotalIncome { get; set; }
    public decimal TotalExpense { get; set; }
    public decimal Balance { get; set; }
    public List<CategoryExpense> ByCategory { get; set; } = [];
}

public class CategoryExpense
{
    public string Name { get; set; } = "";
    public string Color { get; set; } = "";
    public decimal Total { get; set; }
    public decimal PercentOfExpenses { get; set; }
}

public class CategoryInfo
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
}