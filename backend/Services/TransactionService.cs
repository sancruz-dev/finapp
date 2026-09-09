using Dapper;
using FinApp.Api.Data;
using FinApp.Api.Models;

namespace FinApp.Api.Services;

public class TransactionService(DbConnectionFactory db, MerchantNormalizerService merchantService)
{
    // Seleciona colunas explicitamente para evitar colisão entre t.* e aliases do JOIN
    private const string SelectCols = @"
        t.id, t.user_id, t.category_id, t.subcategory_id, t.type, t.amount, t.description, t.date, t.method, t.installment, t.late_processing,
        CASE WHEN t.fixed = 'S' THEN 1 ELSE 0 END AS fixed,
        t.notes, t.details,
        t.created_at, t.updated_at,
        c.name  AS category_name,
        c.color AS category_color,
        c.icon  AS category_icon,
        sc.name  AS subcategory_name,
        sc.color AS subcategory_color";

    // Transação de "processamento tardio": a operadora do cartão só processou a compra no
    // ciclo seguinte (comum perto do fechamento da fatura). Para fins de período no dashboard,
    // sua data efetiva é empurrada 1 mês para frente, então ela aparece no mês seguinte ao real.
    private const string EffectiveDateExprAliased = "IF(t.late_processing = 1, DATE_ADD(t.date, INTERVAL 1 MONTH), t.date)";
    private const string EffectiveDateExprBare = "IF(late_processing = 1, DATE_ADD(date, INTERVAL 1 MONTH), date)";

    public async Task<IEnumerable<Transaction>> ListAsync(
        int userId, DateOnly? periodStart, DateOnly? periodEnd, string? type, int? categoryId)
    {
        var sql = $@"
            SELECT {SelectCols}
            FROM transactions t
            LEFT JOIN categories c  ON t.category_id    = c.id
            LEFT JOIN categories sc ON t.subcategory_id  = sc.id
            WHERE t.user_id = @UserId";

        var p = new DynamicParameters();
        p.Add("UserId", userId);

        if (periodStart.HasValue && periodEnd.HasValue)
        {
            var (incomeStart, incomeEnd) = PeriodCalculator.GetIncomeRange(periodStart.Value, periodEnd.Value);
            sql += $@"
              AND (
                (t.type IN ('expense','refund') AND {EffectiveDateExprAliased} BETWEEN @Start AND @End)
                OR (t.type = 'income' AND t.date BETWEEN @IncomeStart AND @IncomeEnd)
              )";
            p.Add("Start", periodStart.Value.ToDateTime(TimeOnly.MinValue));
            p.Add("End", periodEnd.Value.ToDateTime(TimeOnly.MinValue));
            p.Add("IncomeStart", incomeStart.ToDateTime(TimeOnly.MinValue));
            p.Add("IncomeEnd", incomeEnd.ToDateTime(TimeOnly.MinValue));
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

        // Transação parcelada: a data informada vira o início da parcela 1, e as demais
        // (2..N) são clonadas automaticamente para os meses seguintes.
        var total = InstallmentHelper.ParseTotal(req.Installment);
        var installmentsToCreate = total is > 1 ? total.Value : 1;
        DateTime.TryParse(req.Date, out var baseDate);

        var firstId = 0;
        for (var i = 1; i <= installmentsToCreate; i++)
        {
            var date = installmentsToCreate > 1
                ? baseDate.AddMonths(i - 1).ToString("yyyy-MM-dd")
                : req.Date;
            var installment = installmentsToCreate > 1
                ? InstallmentHelper.Label(i, installmentsToCreate)
                : req.Installment;

            var id = await conn.ExecuteScalarAsync<int>(@"
                INSERT INTO transactions (user_id, type, amount, description, date, category_id, subcategory_id, notes, details, method, installment, late_processing, fixed)
                VALUES (@UserId, @Type, @Amount, @Description, @Date, @CategoryId, @SubcategoryId, @Notes, @Details, @Method, @Installment, @LateProcessing, @Fixed);
                SELECT LAST_INSERT_ID();",
                new
                {
                    UserId = userId,
                    req.Type,
                    req.Amount,
                    req.Description,
                    Date = date,
                    CategoryId = req.CategoryId,
                    SubcategoryId = req.SubcategoryId,
                    Notes = req.Notes,
                    Details = req.Details,
                    Method = req.Method,
                    Installment = installment,
                    req.LateProcessing,
                    Fixed = req.Fixed ? "S" : "N",
                });

            if (i == 1) firstId = id;

            // ── Normalização de comerciante (ML) ──────────────────────────────
            if (!string.IsNullOrWhiteSpace(req.Description))
                _ = Task.Run(() => merchantService.ProcessTransactionAsync(id, req.Description, userId));
            // ─────────────────────────────────────────────────────────────────
        }

        return await conn.QueryFirstOrDefaultAsync<Transaction>($@"
            SELECT {SelectCols}
            FROM transactions t
            LEFT JOIN categories c  ON t.category_id    = c.id
            LEFT JOIN categories sc ON t.subcategory_id  = sc.id
            WHERE t.id = @Id", new { Id = firstId });
    }
    public async Task<bool> UpdateAsync(int id, int userId, UpdateTransactionRequest req)
    {
        using var conn = db.Create();
        var rows = await conn.ExecuteAsync(@"
            UPDATE transactions
            SET type            = @Type,
                amount          = @Amount,
                description     = @Description,
                date            = @Date,
                category_id     = @CategoryId,
                subcategory_id  = @SubcategoryId,
                notes           = @Notes,
                details         = @Details,
                method          = @Method,
                installment     = @Installment,
                late_processing = @LateProcessing,
                fixed           = @Fixed
            WHERE id = @Id AND user_id = @UserId",
            new
            {
                req.Type,
                req.Amount,
                req.Description,
                Date = req.Date,
                CategoryId = req.CategoryId,
                SubcategoryId = req.SubcategoryId,
                Notes = req.Notes,
                Details = req.Details,
                Method = req.Method,
                Installment = req.Installment,
                req.LateProcessing,
                Fixed = req.Fixed ? "S" : "N",
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

    public async Task<SummaryResponse> SummaryAsync(int userId, DateOnly periodStart, DateOnly periodEnd)
    {
        var (incomeStart, incomeEnd) = PeriodCalculator.GetIncomeRange(periodStart, periodEnd);

        using var conn = db.Create();

        var p = new
        {
            UserId = userId,
            Start = periodStart.ToDateTime(TimeOnly.MinValue),
            End = periodEnd.ToDateTime(TimeOnly.MinValue),
            IncomeStart = incomeStart.ToDateTime(TimeOnly.MinValue),
            IncomeEnd = incomeEnd.ToDateTime(TimeOnly.MinValue),
        };

        var totals = await conn.QueryFirstAsync($@"
            SELECT
              SUM(CASE WHEN type = 'income'  THEN amount ELSE 0 END) AS total_income,
              SUM(CASE WHEN type = 'expense' THEN amount
                       WHEN type = 'refund'  THEN -amount
                       ELSE 0 END)                                   AS total_expense,
              SUM(CASE WHEN type = 'income'  THEN  amount
                       WHEN type = 'expense' THEN -amount
                       WHEN type = 'refund'  THEN  amount
                       ELSE 0 END)                                   AS balance,
              SUM(CASE WHEN type = 'income' AND (method IS NULL OR method <> 'vr')
                       THEN amount ELSE 0 END)                       AS income_regular,
              SUM(CASE WHEN type = 'income' AND method = 'vr'
                       THEN amount ELSE 0 END)                       AS income_vr,
              SUM(CASE WHEN (method <> 'vr' OR method IS NULL)
                       THEN (CASE WHEN type = 'expense' THEN amount
                                  WHEN type = 'refund'  THEN -amount
                                  ELSE 0 END)
                       ELSE 0 END)                                   AS expense_regular,
              SUM(CASE WHEN method = 'vr'
                       THEN (CASE WHEN type = 'expense' THEN amount
                                  WHEN type = 'refund'  THEN -amount
                                  ELSE 0 END)
                       ELSE 0 END)                                   AS expense_vr,
              SUM(CASE WHEN method = 'credito'
                       THEN (CASE WHEN type = 'expense' THEN amount
                                  WHEN type = 'refund'  THEN -amount
                                  ELSE 0 END)
                       ELSE 0 END)                                   AS expense_credito,
              SUM(CASE WHEN method IN ('debito','pix')
                       THEN (CASE WHEN type = 'expense' THEN amount
                                  WHEN type = 'refund'  THEN -amount
                                  ELSE 0 END)
                       ELSE 0 END)                                   AS expense_debito_pix,
              SUM(CASE WHEN method = 'cedula'
                       THEN (CASE WHEN type = 'expense' THEN amount
                                  WHEN type = 'refund'  THEN -amount
                                  ELSE 0 END)
                       ELSE 0 END)                                   AS expense_cedula
            FROM transactions
            WHERE user_id = @UserId
              AND (
                (type IN ('expense','refund') AND {EffectiveDateExprBare} BETWEEN @Start AND @End)
                OR (type = 'income' AND date BETWEEN @IncomeStart AND @IncomeEnd)
              )",
            p);

        var incomeRegular = (decimal)(totals.income_regular ?? 0);
        var incomeVr = (decimal)(totals.income_vr ?? 0);
        var expenseRegular = (decimal)(totals.expense_regular ?? 0);
        var expenseVr = (decimal)(totals.expense_vr ?? 0);
        var expenseCredito = (decimal)(totals.expense_credito ?? 0);
        var expenseDebitoPix = (decimal)(totals.expense_debito_pix ?? 0);
        var expenseCedula = (decimal)(totals.expense_cedula ?? 0);

        var byCategory = await conn.QueryAsync<SummaryCategory>($@"
            SELECT
              c.name  AS name,
              c.color AS color,
              SUM(t.amount) AS total
            FROM transactions t
            JOIN categories c ON t.category_id = c.id
            WHERE t.user_id  = @UserId
              AND t.type     = 'expense'
              AND {EffectiveDateExprAliased} BETWEEN @Start AND @End
            GROUP BY c.id
            ORDER BY total DESC",
            p);

        // ── Orçamentos: só categorias de despesa com teto definido, sempre presentes
        //    (mesmo com gasto zero no período) ──────────────────────────────────────
        var budgets = await conn.QueryAsync<CategoryBudget>($@"
            SELECT
              c.id            AS id,
              c.name          AS name,
              c.color         AS color,
              c.monthly_limit AS monthly_limit,
              COALESCE(SUM(CASE WHEN t.type = 'expense' AND {EffectiveDateExprAliased} BETWEEN @Start AND @End THEN t.amount
                                 WHEN t.type = 'refund'  AND {EffectiveDateExprAliased} BETWEEN @Start AND @End THEN -t.amount
                                 ELSE 0 END), 0) AS spent
            FROM categories c
            LEFT JOIN transactions t ON t.category_id = c.id AND t.user_id = @UserId
            WHERE c.user_id = @UserId
              AND c.type = 'expense'
              AND c.monthly_limit IS NOT NULL
            GROUP BY c.id
            ORDER BY c.name",
            p);

        return new SummaryResponse(
            TotalIncome: (decimal)(totals.total_income ?? 0),
            TotalExpense: (decimal)(totals.total_expense ?? 0),
            Balance: (decimal)(totals.balance ?? 0),
            ByCategory: byCategory,
            PeriodStart: periodStart,
            PeriodEnd: periodEnd,
            IncomeRegular: incomeRegular,
            IncomeVr: incomeVr,
            ExpenseRegular: expenseRegular,
            ExpenseVr: expenseVr,
            BalanceRegular: incomeRegular - expenseRegular,
            BalanceVr: incomeVr - expenseVr,
            ExpenseCredito: expenseCredito,
            ExpenseDebitoPix: expenseDebitoPix,
            ExpenseCedula: expenseCedula,
            Budgets: budgets);
    }
}
