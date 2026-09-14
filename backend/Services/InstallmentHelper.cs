using System.Text.RegularExpressions;

namespace FinApp.Api.Services;

/// <summary>
/// Extrai o total de parcelas de um texto livre (ex.: "Parcela 2/4", "2/4", "2 de 4")
/// e gera o rótulo padronizado "Parcela X/Y" usado ao clonar as parcelas futuras.
/// </summary>
public static class InstallmentHelper
{
    private static readonly Regex TotalRegex = new(@"(\d{1,2})\s*(?:/|de)\s*(\d{1,2})", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>Retorna o total de parcelas (Y) se houver mais de 1, senão null.</summary>
    public static int? ParseTotal(string? installment)
    {
        if (string.IsNullOrWhiteSpace(installment)) return null;
        var m = TotalRegex.Match(installment);
        if (!m.Success) return null;
        var total = int.Parse(m.Groups[2].Value);
        return total > 1 ? total : null;
    }

    /// <summary>Retorna a parcela atual (X) e o total (Y) informados (ex.: "2/5" -&gt; (2, 5)), se houver mais de 1 parcela.</summary>
    public static (int Current, int Total)? ParseCurrentAndTotal(string? installment)
    {
        if (string.IsNullOrWhiteSpace(installment)) return null;
        var m = TotalRegex.Match(installment);
        if (!m.Success) return null;
        var current = int.Parse(m.Groups[1].Value);
        var total = int.Parse(m.Groups[2].Value);
        if (total <= 1) return null;
        if (current < 1) current = 1;
        if (current > total) current = total;
        return (current, total);
    }

    public static string Label(int current, int total) => $"Parcela {current}/{total}";
}
