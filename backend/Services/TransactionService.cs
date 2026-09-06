using Dapper;
using FinApp.Api.Data;
using FinApp.Api.Models;

namespace FinApp.Api.Services;

public class TransactionService(DbConnectionFactory db, MerchantNormalizerService merchantService)
{
    // Seleciona colunas explicitamente para evitar colisão entre t.* e aliases do JOIN
    private const string SelectCols = @"
        t.id, t.user_id, t.category_id, t.type, t.amount, t.description, t.date, t.method, t.notes,
        t.created_at, t.updated_at,
        c.name  AS category_name,
        c.color AS category_color,
        c.icon  AS category_icon";

    public async Task<IEnumerable<Transaction>> ListAsync(
        int userId, int? month, int? year, string? type, int? categoryId)
    {
        var sql = $@"
            SELECT {SelectCols}
            FROM transactions t
            LEFT JOIN categories c ON t.category_id = c.id
            WHERE t.user_id = @UserId";

        var p = new DynamicParameters();
        p.Add("UserId", userId);

        if (month.HasValue && year.HasValue)
        {
            sql += " AND MONTH(t.date) = @Month AND YEAR(t.date) = @Year";
            p.Add("Month", month); p.Add("Year", year);
        }
        if (!string.IsNullOrEmpty(type))
        {
            sql += " AND t.type = @Type";
            p.Add("Type", type);
        }
        if (categoryId.HasValue)
        {
            sql += " AND t.category_id = @CategoryId";
            p.Add("CategoryId", categoryId);
        }

        sql += " ORDER BY t.date DESC, t.created_at DESC";

        using var conn = db.Create();
        return await conn.QueryAsync<Transaction>(sql, p);
    }

    public async Task<Transaction?> CreateAsync(int userId, CreateTransactionRequest req)
    {
        using var conn = db.Create();
        var id = await conn.ExecuteScalarAsync<int>(@"
            INSERT INTO transactions (user_id, type, amount, description, date, category_id, notes, method)
            VALUES (@UserId, @Type, @Amount, @Description, @Date, @CategoryId, @Notes, @Method);
            SELECT LAST_INSERT_ID();",
            new
            {
                UserId = userId,
                req.Type,
                req.Amount,
                req.Description,
                Date = req.Date,
                CategoryId = req.CategoryId,
                Notes = req.Notes,
                Method = req.Method,
            });

        // ── Normalização de comerciante (ML) ──────────────────────────────
        if (!string.IsNullOrWhiteSpace(req.Description))
            _ = Task.Run(() => merchantService.ProcessTransactionAsync(id, req.Description, userId));
        // ─────────────────────────────────────────────────────────────────

        return await conn.QueryFirstOrDefaultAsync<Transaction>($@"
            SELECT {SelectCols}
            FROM transactions t
            LEFT JOIN categories c ON t.category_id = c.id
            WHERE t.id = @Id", new { Id = id });
    }
    public async Task<bool> UpdateAsync(int id, int userId, UpdateTransactionRequest req)
    {
        using var conn = db.Create();
        var rows = await conn.ExecuteAsync(@"
            UPDATE transactions
            SET type        = @Type,
                amount      = @Amount,
                description = @Description,
                date        = @Date,
                category_id = @CategoryId,
                notes       = @Notes,
                method      = @Method
            WHERE id = @Id AND user_id = @UserId",
            new
            {
                req.Type,
                req.Amount,
                req.Description,
                Date = req.Date,
                CategoryId = req.CategoryId,
                Notes = req.Notes,
                Method = req.Method,
                Id = id,
                UserId = userId,
            });
        return rows > 0;
    }

    public async Task<bool> DeleteAsync(int id, int userId)
    {
        using var conn = db.Create();
        var rows = await conn.ExecuteAsync(
            "DELETE FROM transactions WHERE id = @Id AND user_id = @UserId",
            new { Id = id, UserId = userId });
        return rows > 0;
    }

    public async Task<SummaryResponse> SummaryAsync(int userId, int month, int year)
    {
        using var conn = db.Create();

        var totals = await conn.QueryFirstAsync(@"
            SELECT
              SUM(CASE WHEN type = 'income'  THEN amount ELSE 0 END) AS total_income,
              SUM(CASE WHEN type = 'expense' THEN amount
                       WHEN type = 'refund'  THEN -amount
                       ELSE 0 END)                                   AS total_expense,
              SUM(CASE WHEN type = 'income'  THEN  amount
                       WHEN type = 'expense' THEN -amount
                       WHEN type = 'refund'  THEN  amount
                       ELSE 0 END)                                   AS balance
            FROM transactions
            WHERE user_id = @UserId
              AND MONTH(date) = @Month
              AND YEAR(date)  = @Year",
            new { UserId = userId, Month = month, Year = year });

        var byCategory = await conn.QueryAsync<SummaryCategory>(@"
            SELECT
              c.name  AS name,
              c.color AS color,
              SUM(t.amount) AS total
            FROM transactions t
            JOIN categories c ON t.category_id = c.id
            WHERE t.user_id  = @UserId
              AND t.type     = 'expense'
              AND MONTH(t.date) = @Month
              AND YEAR(t.date)  = @Year
            GROUP BY c.id
            ORDER BY total DESC",
            new { UserId = userId, Month = month, Year = year });

        return new SummaryResponse(
            TotalIncome: (decimal)(totals.total_income ?? 0),
            TotalExpense: (decimal)(totals.total_expense ?? 0),
            Balance: (decimal)(totals.balance ?? 0),
            ByCategory: byCategory);
    }
}