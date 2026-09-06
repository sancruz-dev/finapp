namespace FinApp.Api.Models;

public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public class Category
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string Color { get; set; } = "#6366f1";
    public string? Icon { get; set; }
    public DateTime CreatedAt { get; set; }

    // preenchido sob demanda
    public List<CategoryKeyword> Keywords { get; set; } = [];
}

public class CategoryKeyword
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public string Keyword { get; set; } = "";
}

public class Transaction
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int? CategoryId { get; set; }
    public string Type { get; set; } = "";
    public decimal Amount { get; set; }
    public string Description { get; set; } = "";
    public DateTime Date { get; set; }
    public string? Method { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // JOINs
    public string? CategoryName { get; set; }
    public string? CategoryColor { get; set; }
    public string? CategoryIcon { get; set; }
}

// ── Auth DTOs ──────────────────────────────────────────────────────────────
public record LoginRequest(string Email, string Password);
public record RegisterRequest(string Name, string Email, string Password);

// ── Transaction DTOs ───────────────────────────────────────────────────────
public record CreateTransactionRequest(
    string Type,
    decimal Amount,
    string Description,
    string Date,
    int? CategoryId,
    string? Notes,
    string? Method);

public record UpdateTransactionRequest(
    string Type,
    decimal Amount,
    string Description,
    string Date,
    int? CategoryId,
    string? Notes,
    string? Method);

// ── Category DTOs ──────────────────────────────────────────────────────────
public record CreateCategoryRequest(
    string Name,
    string Type,
    string? Color,
    string? Icon);

public record AddKeywordRequest(string Keyword);

// ── Summary ────────────────────────────────────────────────────────────────
public record SummaryCategory(string Name, string Color, decimal Total);

public record SummaryResponse(
    decimal TotalIncome,
    decimal TotalExpense,
    decimal Balance,
    IEnumerable<SummaryCategory> ByCategory);

// ── CSV Import ─────────────────────────────────────────────────────────────

/// <summary>Uma linha parseada do CSV antes de ser salva.</summary>
public class CsvRow
{
    public string Description { get; set; } = "";
    public decimal Amount { get; set; }
    public string Type { get; set; } = "expense";   // "income" | "expense"
    public string Date { get; set; } = "";          // "YYYY-MM-DD"
    public string Method { get; set; } = "credito";  // "credito" | "debito" | "pix"
    public int? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string? CategoryColor { get; set; }
    public bool IsInstallment { get; set; } = false;       // data fora do mês dominante
    public bool IsRefund { get; set; } = false;       // reembolso/estorno (valor negativo no CSV)
}

/// <summary>Resultado do parse do CSV devolvido ao frontend para revisão.</summary>
public class CsvPreviewResponse
{
    public List<CsvRow> Rows { get; set; } = [];
    public int Total { get; set; }
    public int Matched { get; set; }   // categorizados automaticamente
    public int Unmatched { get; set; }   // sem categoria
    public string DominantMonth { get; set; } = "";  // "2026-02" — mês que mais aparece
    public int Installments { get; set; }   // lançamentos fora do mês dominante
    public int Refunds { get; set; }   // reembolsos/estornos detectados
}

/// <summary>Payload enviado pelo frontend após revisão para confirmar importação.</summary>
public record ImportConfirmRequest(List<CsvRow> Rows);