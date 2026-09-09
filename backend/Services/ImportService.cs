using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using FinApp.Api.Data;
using FinApp.Api.Models;
using Dapper;
using ClosedXML.Excel;

namespace FinApp.Api.Services;

public class ImportService(DbConnectionFactory db, CategoryService categoryService, MerchantNormalizerService merchantService)
{
    // Tipos de importação suportados: "fatura" (cartão de crédito), "extrato" (conta corrente), "vr" (vale alimentação/refeição)
    public async Task<CsvPreviewResponse> ParseAndMatchAsync(int userId, Stream fileStream, string fileName, string importType = "fatura")
    {
        var catKeywords = await categoryService.GetAllWithKeywordsAsync(userId);
        var rows = new List<CsvRow>();

        var isExcel = fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)
                   || fileName.EndsWith(".xls", StringComparison.OrdinalIgnoreCase);

        var rawRows = isExcel ? ReadExcelRows(fileStream) : await ReadCsvRowsAsync(fileStream);

        var (headerIdx, dateCol, descCol, amountCol, installmentCol) = FindColumns(rawRows);
        var startIdx = headerIdx + 1;
        var minCols = Math.Max(dateCol, Math.Max(descCol, amountCol)) + 1;

        // Fatura: valor negativo = reembolso/estorno. Extrato de conta e VR: valor negativo = despesa,
        // valor positivo = receita (crédito recebido/benefício), já que não são faturas de cartão.
        var isSignedAccount = importType is "extrato" or "vr";
        var defaultMethod = importType switch
        {
            "extrato" => "pix",
            "vr" => "vr",
            _ => "credito",
        };

        for (var i = startIdx; i < rawRows.Count; i++)
        {
            var parts = rawRows[i];
            if (parts.Length < minCols) continue;

            var dateRaw = parts[dateCol].Trim().Trim('"');
            var descRaw = CleanDescription(parts[descCol].Trim().Trim('"'));
            var amountRaw = parts[amountCol].Trim().Trim('"');

            if (!TryParseDate(dateRaw, out var date)) continue;
            if (!TryParseDecimal(amountRaw, out var amount)) continue;
            if (amount == 0) continue;

            var isNegative = amount < 0;

            // Fatura: linha de pagamento do saldo da fatura anterior (ex.: "Pagamento Com Saldo").
            // Não é reembolso nem despesa do período - é só a quitação do débito anterior,
            // já refletida na saída de dinheiro da conta corrente. Ignora a linha.
            if (importType == "fatura" && isNegative && IsInvoicePaymentLine(descRaw))
                continue;
            var type = isSignedAccount
                ? (isNegative ? "expense" : "income")
                : (isNegative ? "refund" : "expense");

            var row = new CsvRow
            {
                Description = descRaw,
                Amount = Math.Abs(amount),
                Type = type,
                Method = defaultMethod,
                IsRefund = !isSignedAccount && isNegative,
                Date = date.ToString("yyyy-MM-dd"),
            };

            // Parcelamento: só se aplica a faturas de cartão (extrato de conta e VR não têm parcelas)
            if (importType == "fatura")
            {
                // Primeiro tenta a coluna dedicada (ex.: Itaú "Parcelamento"),
                // senão procura o padrão no texto da descrição (ex.: Nubank "... Parcela 10/12")
                var installmentRaw = installmentCol != -1 && installmentCol < parts.Length
                    ? parts[installmentCol].Trim().Trim('"')
                    : "";
                row.Installment = !string.IsNullOrWhiteSpace(installmentRaw)
                    ? ExtractInstallment(installmentRaw)
                    : ExtractInstallment(descRaw);
            }

            // Categoriza por keyword (case-insensitive), restrito a categorias do mesmo tipo
            // (receita casa só com categorias de receita; despesa/reembolso com categorias de despesa)
            var categoryType = type == "income" ? "income" : "expense";
            var descUpper = descRaw.ToUpperInvariant();
            foreach (var (cat, keywords) in catKeywords)
            {
                if (cat.Type == categoryType && keywords.Any(kw => descUpper.Contains(kw)))
                {
                    row.CategoryId = cat.Id;
                    row.CategoryName = cat.Name;
                    row.CategoryColor = cat.Color;
                    break;
                }
            }

            rows.Add(row);
        }

        var matched = rows.Count(r => r.CategoryId.HasValue);
        var installments = rows.Count(r => r.Installment != null);
        var refunds = rows.Count(r => r.IsRefund);

        return new CsvPreviewResponse
        {
            Rows = rows,
            Total = rows.Count,
            Matched = matched,
            Unmatched = rows.Count - matched,
            Installments = installments,
            Refunds = refunds,
        };
    }

    /// <summary>
    /// Verifica, para cada linha parcelada a importar, se já existe no banco uma transação
    /// parcelada com a mesma descrição e o mesmo total de parcelas (possível reimportação
    /// da mesma fatura). Não altera nada - só reporta os grupos conflitantes.
    /// </summary>
    public async Task<List<InstallmentDuplicate>> FindInstallmentDuplicatesAsync(int userId, List<CsvRow> rows)
    {
        using var conn = db.Create();
        var result = new List<InstallmentDuplicate>();
        var seen = new HashSet<(string Description, int Total)>();

        foreach (var row in rows)
        {
            var total = InstallmentHelper.ParseTotal(row.Installment);
            if (total is null) continue;

            var descKey = row.Description.Trim().ToUpperInvariant();
            if (!seen.Add((descKey, total.Value))) continue;

            var existing = (await conn.QueryAsync<InstallmentDuplicateExisting>(@"
                SELECT id, description, date, installment, amount
                FROM transactions
                WHERE user_id = @UserId
                  AND installment LIKE @Pattern
                  AND UPPER(TRIM(description)) = @Description",
                new { UserId = userId, Pattern = $"%/{total.Value}", Description = descKey }))
                .ToList();

            if (existing.Count > 0)
                result.Add(new InstallmentDuplicate(row.Description, total.Value, existing));
        }

        return result;
    }

    public async Task<int> ConfirmImportAsync(int userId, List<CsvRow> rows)
    {
        using var conn = db.Create();
        var saved = 0;

        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.Description) || row.Amount <= 0) continue;

            // Transação parcelada: a data da linha importada vira o início da parcela 1,
            // e as demais (2..N) são clonadas automaticamente para os meses seguintes.
            var total = InstallmentHelper.ParseTotal(row.Installment);
            var installmentsToCreate = total is > 1 ? total.Value : 1;
            DateTime.TryParse(row.Date, out var baseDate);

            for (var i = 1; i <= installmentsToCreate; i++)
            {
                var date = installmentsToCreate > 1
                    ? baseDate.AddMonths(i - 1).ToString("yyyy-MM-dd")
                    : row.Date;
                var installment = installmentsToCreate > 1
                    ? InstallmentHelper.Label(i, installmentsToCreate)
                    : row.Installment;

                var id = await conn.ExecuteScalarAsync<int>(@"
                    INSERT INTO transactions (user_id, type, amount, description, date, category_id, subcategory_id, method, installment, details, fixed)
                    VALUES (@UserId, @Type, @Amount, @Description, @Date, @CategoryId, @SubcategoryId, @Method, @Installment, @Details, @Fixed);
                    SELECT LAST_INSERT_ID();",
                    new
                    {
                        UserId = userId,
                        row.Type,
                        row.Amount,
                        row.Description,
                        Date = date,
                        CategoryId = row.CategoryId,
                        SubcategoryId = row.SubcategoryId,
                        Method = string.IsNullOrWhiteSpace(row.Method) ? "credito" : row.Method,
                        Installment = installment,
                        Details = row.Details,
                        Fixed = row.Fixed ? "S" : "N",
                    });
                saved++;

                // ── Normalização de comerciante (ML) ──────────────────────────
                _ = Task.Run(() => merchantService.ProcessTransactionAsync(id, row.Description, userId));
                // ───────────────────────────────────────────────────────────────
            }
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
    private static (int HeaderIdx, int DateCol, int DescCol, int AmountCol, int InstallmentCol) FindColumns(List<string[]> rows)
    {
        for (var r = 0; r < rows.Count; r++)
        {
            var row = rows[r];
            int dateCol = -1, descCol = -1, amountCol = -1, installmentCol = -1;

            for (var c = 0; c < row.Length; c++)
            {
                var norm = NormalizeHeader(row[c]);
                if (dateCol == -1 && (norm == "data" || norm == "date")) dateCol = c;
                else if (descCol == -1 && (norm.Contains("lancamento") || norm.Contains("descricao") || norm.Contains("historico") || norm == "title")) descCol = c;
                else if (amountCol == -1 && (norm == "valor" || norm == "amount")) amountCol = c;
                else if (installmentCol == -1 && (norm.Contains("parcelamento") || norm.Contains("parcela"))) installmentCol = c;
            }

            if (dateCol != -1 && descCol != -1 && amountCol != -1)
                return (r, dateCol, descCol, amountCol, installmentCol);
        }

        // Sem cabeçalho reconhecido: assume layout simples Data, Descrição, Valor
        return (-1, 0, 1, 2, -1);
    }

    // ── Detecção de pagamento de fatura ──────────────────────────────────────
    // Padrões comuns entre bancos para a linha que quita o saldo devedor anterior.
    private static readonly string[] InvoicePaymentKeywords =
    [
        "PAGAMENTO", "PAGTO", "PGTO"
    ];

    private static bool IsInvoicePaymentLine(string description)
    {
        var upper = description.ToUpperInvariant();
        return InvoicePaymentKeywords.Any(kw => upper.Contains(kw));
    }

    // ── Limpeza de descrição ──────────────────────────────────────────────────
    // Extratos bancários trazem transferências com ruído (CPF mascarado, banco, agência/conta):
    // "Transferência Recebida - Fulano de Tal - •••.521.158-•• - NU PAGAMENTOS - IP (0260) Agência: 1 Conta: 68533114-6"
    // "Transferência recebida pelo Pix - Fulano de Tal - •••.384.538-•• - ITAÚ UNIBANCO S.A. (0341) Agência: 8485 Conta: 57203-9"
    // Recebida mantém o rótulo + nome; Enviada mantém só o nome do destinatário. O trecho "pelo Pix" (quando existe) é descartado.
    private static readonly Regex TransferRegex = new(
        @"^(Transfer[eê]ncia\s+(Recebida|Enviada))(?:\s+pelo\s+Pix)?\s*-\s*([^-]+?)\s*-",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static string CleanDescription(string description)
    {
        var match = TransferRegex.Match(description);
        if (!match.Success) return description;

        var name = match.Groups[3].Value.Trim();
        var isRecebida = match.Groups[2].Value.Equals("Recebida", StringComparison.OrdinalIgnoreCase);
        return isRecebida ? $"{match.Groups[1].Value.Trim()} - {name}" : name;
    }

    // ── Detecção de parcelamento ─────────────────────────────────────────────
    // Aceita "Parcela 10/12", "Parcela 1 de 12" (formato Itaú) e o padrão nu "10/12".
    // Sempre normaliza a saída para "Parcela X/Y".
    private static readonly Regex InstallmentSlashRegex = new(@"(?<!\d)(\d{1,2})\s*/\s*(\d{1,2})(?!\d)", RegexOptions.Compiled);
    private static readonly Regex InstallmentDeRegex = new(@"(?<!\d)(\d{1,2})\s*de\s*(\d{1,2})(?!\d)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static string? ExtractInstallment(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var slashMatch = InstallmentSlashRegex.Match(text);
        if (slashMatch.Success && IsPlausibleInstallment(slashMatch))
            return $"Parcela {int.Parse(slashMatch.Groups[1].Value)}/{int.Parse(slashMatch.Groups[2].Value)}";

        var deMatch = InstallmentDeRegex.Match(text);
        if (deMatch.Success && IsPlausibleInstallment(deMatch))
            return $"Parcela {int.Parse(deMatch.Groups[1].Value)}/{int.Parse(deMatch.Groups[2].Value)}";

        return null;
    }

    // Evita falsos positivos (ex.: números de cartão, datas) exigindo parcela atual <= total e total <= 60
    private static bool IsPlausibleInstallment(Match match)
    {
        var n = int.Parse(match.Groups[1].Value);
        var total = int.Parse(match.Groups[2].Value);
        return n >= 1 && total >= 1 && n <= total && total <= 60;
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
