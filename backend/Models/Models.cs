namespace FinApp.Api.Models;

public class User
{
    public int    Id           { get; set; }
    public string Name         { get; set; } = "";
    public string Email        { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public DateTime CreatedAt  { get; set; }
}

public class Category
{
    public int     Id        { get; set; }
    public int?    UserId    { get; set; }
    public string  Name      { get; set; } = "";
    public string  Type      { get; set; } = "";   // "income" | "expense"
    public string  Color     { get; set; } = "#6366f1";
    public string? Icon      { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class Transaction
{
    public int      Id            { get; set; }
    public int      UserId        { get; set; }
    public int?     CategoryId    { get; set; }
    public string   Type          { get; set; } = "";  // "income" | "expense"
    public decimal  Amount        { get; set; }
    public string   Description   { get; set; } = "";
    public DateTime Date          { get; set; }
    public string?  Notes         { get; set; }
    public DateTime CreatedAt     { get; set; }
    public DateTime UpdatedAt     { get; set; }

    // JOINs
    public string?  CategoryName  { get; set; }
    public string?  CategoryColor { get; set; }
    public string?  CategoryIcon  { get; set; }
}

// ── DTOs ──────────────────────────────────────────────────────────────────
public record LoginRequest(string Email, string Password);
public record RegisterRequest(string Name, string Email, string Password);

public record CreateTransactionRequest(
    string   Type,
    decimal  Amount,
    string   Description,
    string   Date,           // "YYYY-MM-DD"
    int?     CategoryId,
    string?  Notes);

public record UpdateTransactionRequest(
    string   Type,
    decimal  Amount,
    string   Description,
    string   Date,
    int?     CategoryId,
    string?  Notes);

public record CreateCategoryRequest(
    string  Name,
    string  Type,
    string? Color,
    string? Icon);

public record SummaryCategory(string Name, string Color, decimal Total);

public record SummaryResponse(
    decimal  TotalIncome,
    decimal  TotalExpense,
    decimal  Balance,
    IEnumerable<SummaryCategory> ByCategory);
