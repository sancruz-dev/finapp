using FinApp.Api.Data;
using FinApp.Api.Models;
using Dapper;

namespace FinApp.Api.Services;

public class ImportService(DbConnectionFactory db, CategoryService categoryService, MerchantNormalizerService merchantService)
{
    public async Task<CsvPreviewResponse> ParseAndMatchAsync(int userId, Stream csvStream)
    {
        var catKeywords = await categoryService.GetAllWithKeywordsAsync(userId);
        var rows = new List<CsvRow>();

        using var reader = new StreamReader(csvStream);
        string? line;
        bool firstLine = true;

        while ((line = await reader.ReadLineAsync()) != null)
        {
            line = line.Trim();
            if (string.IsNullOrEmpty(line)) continue;

            var sep = line.Contains(';') ? ';' : ',';
            var parts = line.Split(sep);
            if (parts.Length < 3) continue;

            if (firstLine)
            {
                firstLine = false;
                if (!TryParseDate(parts[0].Trim().Trim('"'), out _)) continue;
            }

            var dateRaw = parts[0].Trim().Trim('"');
            var descRaw = parts[1].Trim().Trim('"');
            var amountRaw = parts[2].Trim().Trim('"');

            if (!TryParseDate(dateRaw, out var date)) continue;
            if (!TryParseDecimal(amountRaw, out var amount)) continue;
            if (amount == 0) continue;

            var isRefund = amount < 0;
            var row = new CsvRow
            {
                Description = descRaw,
                Amount = Math.Abs(amount),
                Type = isRefund ? "refund" : "expense",
                IsRefund = isRefund,
                Date = date.ToString("yyyy-MM-dd"),
            };

            // Categoriza por keyword (case-insensitive)
            var descUpper = descRaw.ToUpperInvariant();
            foreach (var (cat, keywords) in catKeywords)
            {
                if (keywords.Any(kw => descUpper.Contains(kw)))
                {
                    row.CategoryId = cat.Id;
                    row.CategoryName = cat.Name;
                    row.CategoryColor = cat.Color;
                    break;
                }
            }

            rows.Add(row);
        }

        // Detecta mês dominante e marca parcelas
        var dominantMonth = DetectDominantMonth(rows);
        if (!string.IsNullOrEmpty(dominantMonth))
            foreach (var row in rows)
                row.IsInstallment = !row.Date.StartsWith(dominantMonth);

        var matched = rows.Count(r => r.CategoryId.HasValue);
        var installments = rows.Count(r => r.IsInstallment);
        var refunds = rows.Count(r => r.IsRefund);

        return new CsvPreviewResponse
        {
            Rows = rows,
            Total = rows.Count,
            Matched = matched,
            Unmatched = rows.Count - matched,
            DominantMonth = dominantMonth,
            Installments = installments,
            Refunds = refunds,
        };
    }

    public async Task<int> ConfirmImportAsync(int userId, List<CsvRow> rows)
    {
        using var conn = db.Create();
        var saved = 0;

        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.Description) || row.Amount <= 0) continue;

            var id = await conn.ExecuteScalarAsync<int>(@"
                INSERT INTO transactions (user_id, type, amount, description, date, category_id)
                VALUES (@UserId, @Type, @Amount, @Description, @Date, @CategoryId);
                SELECT LAST_INSERT_ID();",
                new
                {
                    UserId = userId,
                    row.Type,
                    row.Amount,
                    row.Description,
                    Date = row.Date,
                    CategoryId = row.CategoryId,
                });
            saved++;

            // ── Normalização de comerciante (ML) ──────────────────────────
            _ = Task.Run(() => merchantService.ProcessTransactionAsync(id, row.Description, userId));
            // ───────────────────────────────────────────────────────────────
        }

        return saved;
    }

    // ── Mês dominante ──────────────────────────────────────────────────────
    private static string DetectDominantMonth(List<CsvRow> rows)
    {
        if (rows.Count == 0) return "";
        var monthCounts = rows
            .GroupBy(r => r.Date[..7])
            .Select(g => (Month: g.Key, Count: g.Count()))
            .OrderByDescending(x => x.Count)
            .ToList();
        var dominant = monthCounts.First();
        return dominant.Count > rows.Count / 2 ? dominant.Month : "";
    }

    // ── Decimal parsing ────────────────────────────────────────────────────
    /// <summary>
    /// Preserva o sinal negativo antes de normalizar separadores,
    /// garantindo que valores negativos do CSV resultem em amount negativo.
    /// </summary>
    private static bool TryParseDecimal(string raw, out decimal result)
    {
        result = 0;
        raw = raw.Trim().Replace(" ", "").Replace("R$", "");

        // Captura o sinal ANTES de remover o hífen
        var isNegative = raw.StartsWith("-");
        raw = raw.Replace("-", "");

        if (string.IsNullOrEmpty(raw)) return false;

        bool ok;

        if (raw.Contains(','))
        {
            // Formato BR: ponto = milhar, vírgula = decimal  ex: "1.234,56" ou "23,50"
            var normalized = raw.Replace(".", "").Replace(",", ".");
            ok = decimal.TryParse(normalized,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out result);
        }
        else
        {
            var dotCount = raw.Count(c => c == '.');
            if (dotCount == 1)
            {
                var afterDot = raw[(raw.IndexOf('.') + 1)..];
                // 3 dígitos após ponto = separador de milhar (ex: "1.234"), senão = decimal (ex: "23.50")
                if (afterDot.Length == 3)
                    raw = raw.Replace(".", "");
            }
            else if (dotCount > 1)
            {
                raw = raw.Replace(".", "");
            }

            ok = decimal.TryParse(raw,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out result);
        }

        if (ok && isNegative) result = -result;
        return ok;
    }

    // ── Date parsing ───────────────────────────────────────────────────────
    private static readonly string[] DateFormats =
    [
        "yyyy-MM-dd", "dd/MM/yyyy", "MM/dd/yyyy",
        "dd-MM-yyyy", "MM-dd-yyyy", "yyyy/MM/dd",
    ];

    private static bool TryParseDate(string raw, out DateTime result)
        => DateTime.TryParseExact(raw, DateFormats,
               System.Globalization.CultureInfo.InvariantCulture,
               System.Globalization.DateTimeStyles.None, out result)
           || DateTime.TryParse(raw, out result);
}