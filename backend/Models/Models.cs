namespace FinApp.Api.Models;

public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public int ClosingDay { get; set; } = 1;
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
    public decimal? MonthlyLimit { get; set; }
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
    public int? SubcategoryId { get; set; }
    public string Type { get; set; } = "";
    public decimal Amount { get; set; }
    public string Description { get; set; } = "";
    public DateTime Date { get; set; }
    public string? Method { get; set; }
    public string? Installment { get; set; }
    public bool LateProcessing { get; set; }
    public bool Fixed { get; set; }
    public string? Notes { get; set; }
    public string? Details { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // JOINs
    public string? CategoryName { get; set; }
    public string? CategoryColor { get; set; }
    public string? CategoryIcon { get; set; }
    public string? SubcategoryName { get; set; }
    public string? SubcategoryColor { get; set; }
}

// ── Investment ─────────────────────────────────────────────────────────────
public class Investment
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Institution { get; set; } = "";
    public string AssetType { get; set; } = "";   // CDB | LCI | LCA | TESOURO | POUPANCA
    public string Indexer { get; set; } = "";     // CDI | SELIC | PREFIXADO | POUPANCA
    public decimal? IndexerRate { get; set; }     // 110 = 110% CDI; taxa a.a. p/ Prefixado; null p/ Poupança
    public decimal PrincipalAmount { get; set; }
    public DateTime AppliedAt { get; set; }
    public DateTime? MaturityAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class InvestmentMovement
{
    public int Id { get; set; }
    public int InvestmentId { get; set; }
    public string Type { get; set; } = "";   // APORTE | RESGATE
    public decimal Amount { get; set; }
    public DateTime MovementDate { get; set; }
    public DateTime CreatedAt { get; set; }
}

// ── Investment DTOs ────────────────────────────────────────────────────────
public record CreateInvestmentRequest(
    string Institution,
    string AssetType,
    string Indexer,
    decimal? IndexerRate,
    decimal PrincipalAmount,
    string AppliedAt,
    string? MaturityAt);

public record UpdateInvestmentRequest(
    string Institution,
    string AssetType,
    string Indexer,
    decimal? IndexerRate,
    decimal PrincipalAmount,
    string AppliedAt,
    string? MaturityAt);

public record CreateMovementRequest(
    string Type,
    decimal Amount,
    string Date);

public record MovementResponse(
    int Id,
    string Type,
    decimal Amount,
    DateTime Date);

public record InvestmentResponse(
    int Id,
    string Institution,
    string AssetType,
    string Indexer,
    decimal? IndexerRate,
    decimal PrincipalAmount,
    DateTime AppliedAt,
    DateTime? MaturityAt,
    decimal GrossValue,
    decimal NetValue,
    decimal NetContributed,
    List<MovementResponse> Movements);

public record InvestmentSummaryResponse(
    decimal TotalPrincipal,
    decimal TotalGross,
    decimal TotalNet);

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
    string? Method,
    string? Installment,
    int? SubcategoryId,
    string? Details,
    bool LateProcessing = false,
    bool Fixed = false);

public record UpdateTransactionRequest(
    string Type,
    decimal Amount,
    string Description,
    string Date,
    int? CategoryId,
    string? Notes,
    string? Method,
    string? Installment,
    int? SubcategoryId,
    string? Details,
    bool LateProcessing = false,
    bool Fixed = false);

// ── User DTOs ──────────────────────────────────────────────────────────────
public record UpdateClosingDayRequest(int ClosingDay);
public record UpdateProfileRequest(string Name);
public record UpdatePasswordRequest(string CurrentPassword, string NewPassword);

// ── Category DTOs ──────────────────────────────────────────────────────────
public record CreateCategoryRequest(
    string Name,
    string Type,
    string? Color,
    string? Icon);

public record AddKeywordRequest(string Keyword);
public record UpdateMonthlyLimitRequest(decimal? MonthlyLimit);

// ── Summary ────────────────────────────────────────────────────────────────
public record SummaryCategory(string Name, string Color, decimal Total);

public record CategoryBudget(int Id, string Name, string Color, decimal MonthlyLimit, decimal Spent);

public record SummaryResponse(
    decimal TotalIncome,
    decimal TotalExpense,
    decimal Balance,
    IEnumerable<SummaryCategory> ByCategory,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    // ── Quebra por método: VR (vale alimentação/refeição) vs demais métodos ──
    decimal IncomeRegular,
    decimal IncomeVr,
    decimal ExpenseRegular,
    decimal ExpenseVr,
    decimal BalanceRegular,
    decimal BalanceVr,
    // ── Despesas por método de pagamento (crédito, débito/pix, VR, cédula) ──
    decimal ExpenseCredito,
    decimal ExpenseDebitoPix,
    decimal ExpenseCedula,
    // ── Orçamentos: categorias com teto mensal definido ───────────────────
    IEnumerable<CategoryBudget> Budgets);

// ── CSV Import ─────────────────────────────────────────────────────────────

/// <summary>Uma linha parseada do CSV antes de ser salva.</summary>
public class CsvRow
{
    public string Description { get; set; } = "";
    public decimal Amount { get; set; }
    public string Type { get; set; } = "expense";   // "income" | "expense"
    public string Date { get; set; } = "";          // "YYYY-MM-DD"
    public string Method { get; set; } = "credito";  // "credito" | "debito" | "pix" | "vr" | "cedula"
    public int? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string? CategoryColor { get; set; }
    public int? SubcategoryId { get; set; }
    public string? SubcategoryName { get; set; }
    public string? SubcategoryColor { get; set; }
    public string? Installment { get; set; }       // ex.: "Parcela 10/12", detectado por coluna ou texto
    public string? Details { get; set; }            // detalhamento livre (ex.: itens de uma compra variada)
    public bool IsRefund { get; set; } = false;       // reembolso/estorno (valor negativo no CSV)
    public bool Fixed { get; set; } = false;          // transação fixa (aluguel, assinaturas, etc.)
}

/// <summary>Resultado do parse do CSV devolvido ao frontend para revisão.</summary>
public class CsvPreviewResponse
{
    public List<CsvRow> Rows { get; set; } = [];
    public int Total { get; set; }
    public int Matched { get; set; }   // categorizados automaticamente
    public int Unmatched { get; set; }   // sem categoria
    public int Installments { get; set; }   // lançamentos com parcelamento detectado
    public int Refunds { get; set; }   // reembolsos/estornos detectados
}

/// <summary>Payload enviado pelo frontend após revisão para confirmar importação.</summary>
/// <param name="Force">Ignora a checagem de parcelamento duplicado (usado após o usuário decidir manter ou remover).</param>
public record ImportConfirmRequest(List<CsvRow> Rows, bool Force = false);

// ── Duplicidade de parcelamento ───────────────────────────────────────────
public record InstallmentDuplicateExisting(int Id, string Description, DateTime Date, string? Installment, decimal Amount);
public record InstallmentDuplicate(string Description, int Total, List<InstallmentDuplicateExisting> Existing);