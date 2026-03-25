using Dapper;
using FinApp.Api.Data;
using FinApp.Api.Models;

namespace FinApp.Api.Services;

/// <summary>
/// Plugin responsável por buscar e estruturar os dados financeiros do usuário
/// para compor o contexto enviado ao modelo de IA.
/// </summary>
public class FinancialContextPlugin(DbConnectionFactory db)
{
    private static readonly string[] MonthNames =
    [
        "Janeiro", "Fevereiro", "Março", "Abril", "Maio", "Junho",
        "Julho", "Agosto", "Setembro", "Outubro", "Novembro", "Dezembro"
    ];

    /// <summary>
    /// Monta o contexto financeiro completo: mês atual + 12 meses + categorias + comerciantes.
    /// </summary>
    public async Task<FinancialContext> BuildContextAsync(int userId, string userName)
    {
        var now     = DateTime.Now;
        var context = new FinancialContext { UserName = userName };

        // Mês atual
        context.CurrentMonth = await GetMonthSummaryAsync(userId, now.Year, now.Month);

        // Últimos 12 meses (excluindo o atual)
        for (int i = 1; i <= 12; i++)
        {
            var d       = now.AddMonths(-i);
            var summary = await GetMonthSummaryAsync(userId, d.Year, d.Month);
            context.Last12Months.Add(summary);
        }

        // Categorias ativas do usuário
        context.Categories = await GetCategoriesAsync(userId);

        // Gastos por comerciante — mês atual e últimos 3 meses
        context.MerchantSpending = await GetMerchantSpendingAsync(userId, now);

        return context;
    }

    private async Task<MonthSummary> GetMonthSummaryAsync(int userId, int year, int month)
    {
        using var conn = db.Create();

        var totals = await conn.QueryFirstAsync(@"
            SELECT
              COALESCE(SUM(CASE WHEN type = 'income'  THEN amount ELSE 0 END), 0) AS total_income,
              COALESCE(SUM(CASE WHEN type = 'expense' THEN amount
                               WHEN type = 'refund'  THEN -amount
                               ELSE 0 END), 0)                                    AS total_expense,
              COALESCE(SUM(CASE WHEN type = 'income'  THEN  amount
                               WHEN type = 'expense' THEN -amount
                               WHEN type = 'refund'  THEN  amount
                               ELSE 0 END), 0)                                    AS balance
            FROM transactions
            WHERE user_id = @UserId
              AND MONTH(date) = @Month
              AND YEAR(date)  = @Year",
            new { UserId = userId, Month = month, Year = year });

        var byCategory = (await conn.QueryAsync<CategoryExpense>(@"
            SELECT
              c.name  AS name,
              c.color AS color,
              SUM(t.amount) AS total
            FROM transactions t
            JOIN categories c ON t.category_id = c.id
            WHERE t.user_id     = @UserId
              AND t.type        = 'expense'
              AND MONTH(t.date) = @Month
              AND YEAR(t.date)  = @Year
            GROUP BY c.id
            ORDER BY total DESC",
            new { UserId = userId, Month = month, Year = year })).ToList();

        decimal totalExpense = (decimal)totals.total_expense;

        // Calcula percentual de cada categoria
        foreach (var cat in byCategory)
            cat.PercentOfExpenses = totalExpense > 0
                ? Math.Round(cat.Total / totalExpense * 100, 1)
                : 0;

        return new MonthSummary
        {
            Month        = $"{year}-{month:D2}",
            MonthLabel   = $"{MonthNames[month - 1]}/{year}",
            TotalIncome  = (decimal)totals.total_income,
            TotalExpense = totalExpense,
            Balance      = (decimal)totals.balance,
            ByCategory   = byCategory,
        };
    }

    private async Task<List<CategoryInfo>> GetCategoriesAsync(int userId)
    {
        using var conn = db.Create();
        var cats = await conn.QueryAsync<CategoryInfo>(@"
            SELECT name, type
            FROM categories
            WHERE user_id = @UserId
            ORDER BY type, name",
            new { UserId = userId });
        return cats.ToList();
    }

    /// <summary>
    /// Busca gastos agrupados por comerciante para os últimos 3 meses + mês atual.
    /// Só inclui transações que já têm merchant_id resolvido.
    /// </summary>
    private async Task<List<MerchantMonthSpending>> GetMerchantSpendingAsync(int userId, DateTime now)
    {
        using var conn = db.Create();

        var rows = await conn.QueryAsync<MerchantMonthSpending>(@"
            SELECT
                m.name                          AS merchant_name,
                YEAR(t.date)                    AS year,
                MONTH(t.date)                   AS month,
                COUNT(*)                        AS purchase_count,
                SUM(t.amount)                   AS total_spent
            FROM transactions t
            JOIN merchants m ON m.id = t.merchant_id
            WHERE t.user_id    = @UserId
              AND t.type       = 'expense'
              AND t.merchant_id IS NOT NULL
              AND t.date       >= @Since
            GROUP BY m.id, m.name, YEAR(t.date), MONTH(t.date)
            ORDER BY m.name, t.date DESC",
            new {
                UserId = userId,
                Since  = new DateTime(now.Year, now.Month, 1).AddMonths(-3)
            });

        return rows.ToList();
    }

    /// <summary>
    /// Serializa o contexto financeiro em texto estruturado para o system prompt.
    /// </summary>
    public static string BuildSystemPrompt(FinancialContext ctx)
    {
        var fmt = (decimal v) =>
            v.ToString("C", new System.Globalization.CultureInfo("pt-BR"));

        var sb = new System.Text.StringBuilder();

        sb.AppendLine("Você é um assistente financeiro pessoal inteligente e direto.");
        sb.AppendLine("Responda sempre em português, de forma clara e objetiva.");
        sb.AppendLine("Use os dados reais abaixo para dar insights precisos. Nunca invente valores.");
        sb.AppendLine("Quando identificar padrões ou riscos, seja proativo em apontá-los.");
        sb.AppendLine();
        sb.AppendLine($"=== DADOS FINANCEIROS DE {ctx.UserName.ToUpper()} ===");
        sb.AppendLine();

        // ── Mês atual ────────────────────────────────────────────────────────
        var cm            = ctx.CurrentMonth;
        var today         = DateTime.Now;
        var daysInMonth   = DateTime.DaysInMonth(today.Year, today.Month);
        var daysRemaining = daysInMonth - today.Day;
        var dailyBurnRate = today.Day > 0 ? cm.TotalExpense / today.Day : 0;
        var projectedExpense = dailyBurnRate * daysInMonth;
        var projectedBalance = cm.TotalIncome - projectedExpense;

        sb.AppendLine($"--- MÊS ATUAL: {cm.MonthLabel} ---");
        sb.AppendLine($"Receita:  {fmt(cm.TotalIncome)}");
        sb.AppendLine($"Despesas: {fmt(cm.TotalExpense)}");
        sb.AppendLine($"Saldo:    {fmt(cm.Balance)}");
        sb.AppendLine($"Dias restantes no mês: {daysRemaining}");
        sb.AppendLine($"Ritmo diário de gastos: {fmt(dailyBurnRate)}/dia");
        sb.AppendLine($"Projeção de despesas até o fim do mês: {fmt(projectedExpense)}");
        sb.AppendLine($"Saldo projetado ao fim do mês: {fmt(projectedBalance)}");
        sb.AppendLine();

        if (cm.ByCategory.Count > 0)
        {
            sb.AppendLine("Gastos por categoria este mês:");
            foreach (var cat in cm.ByCategory)
                sb.AppendLine($"  - {cat.Name}: {fmt(cat.Total)} ({cat.PercentOfExpenses}% das despesas)");
            sb.AppendLine();
        }

        // ── Histórico 12 meses ───────────────────────────────────────────────
        if (ctx.Last12Months.Count > 0)
        {
            sb.AppendLine("--- HISTÓRICO DOS ÚLTIMOS 12 MESES ---");
            foreach (var m in ctx.Last12Months)
            {
                sb.Append($"  {m.MonthLabel}: Receita {fmt(m.TotalIncome)} | Despesa {fmt(m.TotalExpense)} | Saldo {fmt(m.Balance)}");
                if (m.ByCategory.Count > 0)
                {
                    var top = m.ByCategory.Take(3).Select(c => $"{c.Name} {fmt(c.Total)}");
                    sb.Append($" | Top: {string.Join(", ", top)}");
                }
                sb.AppendLine();
            }
            sb.AppendLine();

            // Métricas calculadas para facilitar insights
            var monthsWithExpense = ctx.Last12Months.Where(m => m.TotalExpense > 0).ToList();
            if (monthsWithExpense.Count > 0)
            {
                var avgExpense = monthsWithExpense.Average(m => m.TotalExpense);
                var avgIncome  = ctx.Last12Months.Where(m => m.TotalIncome > 0).Average(m => m.TotalIncome);
                var avgBalance = ctx.Last12Months.Average(m => m.Balance);
                sb.AppendLine("Médias históricas (12 meses):");
                sb.AppendLine($"  - Despesa média mensal: {fmt(avgExpense)}");
                sb.AppendLine($"  - Receita média mensal: {fmt(avgIncome)}");
                sb.AppendLine($"  - Saldo médio mensal:   {fmt(avgBalance)}");

                if (avgExpense > 0)
                {
                    var variacao = ((cm.TotalExpense - avgExpense) / avgExpense) * 100;
                    var sinal    = variacao >= 0 ? "+" : "";
                    sb.AppendLine($"  - Despesas atuais vs média: {sinal}{variacao:F1}%");
                }
                sb.AppendLine();
            }
        }

        // ── Gastos por comerciante ───────────────────────────────────────────
        if (ctx.MerchantSpending.Count > 0)
        {
            sb.AppendLine("--- GASTOS POR COMERCIANTE (últimos 4 meses) ---");
            sb.AppendLine("Formato: Comerciante | Mês | Qtd compras | Total gasto");
            sb.AppendLine();

            // Agrupa por comerciante para exibição organizada
            var byMerchant = ctx.MerchantSpending
                .GroupBy(x => x.MerchantName)
                .OrderByDescending(g => g.Sum(x => x.TotalSpent));

            foreach (var merchant in byMerchant)
            {
                sb.AppendLine($"  {merchant.Key}:");
                foreach (var row in merchant.OrderByDescending(r => r.Year).ThenByDescending(r => r.Month))
                {
                    var label = $"{MonthNames[row.Month - 1]}/{row.Year}";
                    sb.AppendLine($"    - {label}: {row.PurchaseCount}x | {fmt(row.TotalSpent)}");
                }

                // Total acumulado no período
                var totalPeriod = merchant.Sum(x => x.TotalSpent);
                var totalCount  = merchant.Sum(x => x.PurchaseCount);
                sb.AppendLine($"    Total no período: {totalCount}x | {fmt(totalPeriod)}");
                sb.AppendLine();
            }
        }
        else
        {
            sb.AppendLine("--- GASTOS POR COMERCIANTE ---");
            sb.AppendLine("Nenhum comerciante normalizado ainda. Os lançamentos ainda não foram associados a comerciantes.");
            sb.AppendLine();
        }

        // ── Categorias ───────────────────────────────────────────────────────
        if (ctx.Categories.Count > 0)
        {
            var incomes  = ctx.Categories.Where(c => c.Type == "income").Select(c => c.Name);
            var expenses = ctx.Categories.Where(c => c.Type == "expense").Select(c => c.Name);
            sb.AppendLine("--- CATEGORIAS CONFIGURADAS ---");
            sb.AppendLine($"Receitas:  {string.Join(", ", incomes)}");
            sb.AppendLine($"Despesas: {string.Join(", ", expenses)}");
            sb.AppendLine();
        }

        sb.AppendLine("=== FIM DOS DADOS ===");
        return sb.ToString();
    }
}