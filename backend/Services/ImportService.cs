using System.Globalization;
using System.Text;
using FinApp.Api.Data;
using FinApp.Api.Models;
using Dapper;
using ClosedXML.Excel;

namespace FinApp.Api.Services;

public class ImportService(DbConnectionFactory db, CategoryService categoryService, MerchantNormalizerService merchantService)
{
    public async Task<CsvPreviewResponse> ParseAndMatchAsync(int userId, Stream fileStream, string fileName)
    {
        var catKeywords = await categoryService.GetAllWithKeywordsAsync(userId);
        var rows = new List<CsvRow>();

        var isExcel = fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)
                   || fileName.EndsWith(".xls", StringComparison.OrdinalIgnoreCase);

        var rawRows = isExcel ? ReadExcelRows(fileStream) : await ReadCsvRowsAsync(fileStream);

        var (headerIdx, dateCol, descCol, amountCol) = FindColumns(rawRows);
        var startIdx = headerIdx + 1;
        var minCols = Math.Max(dateCol, Math.Max(descCol, amountCol)) + 1;

        for (var i = startIdx; i < rawRows.Count; i++)
        {
            var parts = rawRows[i];
            if (parts.Length < minCols) continue;

            var dateRaw = parts[dateCol].Trim().Trim('"');
            var descRaw = parts[descCol].Trim().Trim('"');
            var amountRaw = parts[amountCol].Trim().Trim('"');

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

    // ── Leitura de arquivo ────────────────────────────────────────────────
    private static async Task<List<string[]>> ReadCsvRowsAsync(Stream csvStream)
    {
        var result = new List<string[]>();
        using var reader = new StreamReader(csvStream);
        string? line;

        while ((line = await reader.ReadLineAsync()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var sep = line.Contains(';') && !line.TrimStart().StartsWith("\"") ? ';'
                     : line.Count(c => c == ';') > line.Count(c => c == ',') ? ';' : ',';

            result.Add(ParseCsvLine(line, sep));
        }

        return result;
    }

    /// <summary>Faz split respeitando campos entre aspas (que podem conter o separador).</summary>
    private static string[] ParseCsvLine(string line, char sep)
    {
        var fields = new List<string>();
        var sb = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    sb.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == sep && !inQuotes)
            {
                fields.Add(sb.ToString());
                sb.Clear();
            }
            else
            {
                sb.Append(c);
            }
        }
        fields.Add(sb.ToString());
        return fields.ToArray();
    }

    private static List<string[]> ReadExcelRows(Stream excelStream)
    {
        var result = new List<string[]>();
        using var workbook = new XLWorkbook(excelStream);
        var sheet = workbook.Worksheets.First();
        var usedRange = sheet.RangeUsed();
        if (usedRange is null) return result;

        var lastCol = usedRange.LastColumn().ColumnNumber();

        foreach (var xlRow in usedRange.RowsUsed())
        {
            var parts = new string[lastCol];
            for (var c = 1; c <= lastCol; c++)
            {
                var cell = xlRow.Cell(c);
                if (cell.DataType == XLDataType.DateTime)
                    parts[c - 1] = cell.GetDateTime().ToString("yyyy-MM-dd");
                else if (cell.DataType == XLDataType.Number)
                    parts[c - 1] = cell.GetDouble().ToString(CultureInfo.InvariantCulture);
                else
                    parts[c - 1] = cell.GetFormattedString().Trim();
            }
            result.Add(parts);
        }

        return result;
    }

    // ── Detecção de colunas (cabeçalho pode variar de posição/formato) ─────
    private static (int HeaderIdx, int DateCol, int DescCol, int AmountCol) FindColumns(List<string[]> rows)
    {
        for (var r = 0; r < rows.Count; r++)
        {
            var row = rows[r];
            int dateCol = -1, descCol = -1, amountCol = -1;

            for (var c = 0; c < row.Length; c++)
            {
                var norm = NormalizeHeader(row[c]);
                if (dateCol == -1 && norm == "data") dateCol = c;
                else if (descCol == -1 && (norm.Contains("lancamento") || norm.Contains("descricao") || norm.Contains("historico"))) descCol = c;
                else if (amountCol == -1 && norm == "valor") amountCol = c;
            }

            if (dateCol != -1 && descCol != -1 && amountCol != -1)
                return (r, dateCol, descCol, amountCol);
        }

        // Sem cabeçalho reconhecido: assume layout simples Data, Descrição, Valor
        return (-1, 0, 1, 2);
    }

    private static string NormalizeHeader(string raw)
    {
        var trimmed = raw.Trim().Trim('"').Trim();
        var normalized = trimmed.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in normalized)
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        return sb.ToString().ToLowerInvariant();
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
    /// Preserva o sinal negativo antes de normalizar separadores, e decide qual
    /// caractere é o separador decimal pela posição mais à direita entre ',' e '.'
    /// (ex.: "1.234,56" = BR, "1,234.56" = US, "23.50" e "23,50" = decimal simples).
    /// </summary>
    private static bool TryParseDecimal(string raw, out decimal result)
    {
        result = 0;
        raw = raw.Trim().Replace(" ", "").Replace("R$", "");

        // Captura o sinal ANTES de remover o hífen
        var isNegative = raw.StartsWith("-");
        raw = raw.TrimStart('-', '+');

        if (string.IsNullOrEmpty(raw)) return false;

        var lastComma = raw.LastIndexOf(',');
        var lastDot = raw.LastIndexOf('.');

        string normalized;
        if (lastComma != -1 && lastDot != -1)
        {
            // Ambos presentes: o que aparecer por último é o separador decimal
            normalized = lastComma > lastDot
                ? raw.Replace(".", "").Replace(",", ".")
                : raw.Replace(",", "");
        }
        else if (lastComma != -1)
        {
            normalized = raw.Replace(".", "").Replace(",", ".");
        }
        else if (lastDot != -1)
        {
            var dotCount = raw.Count(c => c == '.');
            var afterDot = raw[(lastDot + 1)..];
            // Mais de um ponto, ou 3 dígitos após o único ponto = separador de milhar
            normalized = (dotCount > 1 || afterDot.Length == 3)
                ? raw.Replace(".", "")
                : raw;
        }
        else
        {
            normalized = raw;
        }

        var ok = decimal.TryParse(normalized,
            NumberStyles.Any,
            CultureInfo.InvariantCulture,
            out result);

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
               CultureInfo.InvariantCulture,
               System.Globalization.DateTimeStyles.None, out result)
           || DateTime.TryParse(raw, CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out result);
}
