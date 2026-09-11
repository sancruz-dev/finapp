using FinApp.Api.Models;

namespace FinApp.Api.Services;

/// <summary>
/// Calcula o valor bruto e líquido atual de um investimento a partir das taxas históricas
/// do indexador — nada é persistido, o valor é sempre recalculado na leitura.
/// </summary>
public class InvestmentCalculationService(BacenRateService rates)
{
    public async Task<(decimal Gross, decimal Net)> CalculateAsync(Investment inv)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var appliedAt = DateOnly.FromDateTime(inv.AppliedAt);

        var gross = appliedAt >= today
            ? inv.PrincipalAmount
            : await CalculateGrossAsync(inv, appliedAt, today);

        var net = CalculateNet(inv, gross, appliedAt, today);
        return (Math.Round(gross, 2), Math.Round(net, 2));
    }

    private async Task<decimal> CalculateGrossAsync(Investment inv, DateOnly appliedAt, DateOnly today)
        => inv.Indexer switch
        {
            "CDI" or "SELIC" => await CompoundDailyAsync(inv, appliedAt, today),
            "PREFIXADO" => CompoundPrefixado(inv, appliedAt, today),
            "POUPANCA" => await CompoundPoupancaAsync(inv, appliedAt, today),
            _ => throw new ArgumentException($"Indexador não suportado: {inv.Indexer}")
        };

    // CDI/Selic: juros compostos dia a dia usando a série diária do Bacen × % contratado.
    private async Task<decimal> CompoundDailyAsync(Investment inv, DateOnly appliedAt, DateOnly today)
    {
        var dailyRates = await rates.GetDailyRatesAsync(inv.Indexer, appliedAt, today);
        var pct = (inv.IndexerRate ?? 100m) / 100m;

        var factor = 1m;
        decimal? lastKnownRate = null;
        var hasToday = false;
        foreach (var (date, rate) in dailyRates)
        {
            if (date <= appliedAt || date > today) continue;
            factor *= 1 + (rate / 100m) * pct;
            lastKnownRate = rate;
            if (date == today) hasToday = true;
        }

        // O Bacen publica a taxa do dia útil com atraso — projeta o dia corrente com a última taxa
        // conhecida (como os bancos fazem); o valor se autoajusta quando a taxa oficial sai. Não
        // projeta em fins de semana (sem pregão, sem taxa a publicar).
        var isWeekend = today.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
        if (!hasToday && !isWeekend && lastKnownRate is not null)
            factor *= 1 + (lastKnownRate.Value / 100m) * pct;

        return inv.PrincipalAmount * factor;
    }

    // Prefixado: sem série externa — juros compostos por dias corridos sobre a taxa anual contratada.
    private static decimal CompoundPrefixado(Investment inv, DateOnly appliedAt, DateOnly today)
    {
        var days = today.DayNumber - appliedAt.DayNumber;
        var annualRate = (double)(inv.IndexerRate ?? 0m) / 100.0;
        var factor = Math.Pow(1 + annualRate, days / 365.0);
        return inv.PrincipalAmount * (decimal)factor;
    }

    // Poupança: só credita rendimento a cada "aniversário" de 30 dias corridos (regra real da caderneta).
    private async Task<decimal> CompoundPoupancaAsync(Investment inv, DateOnly appliedAt, DateOnly today)
    {
        var periods = await rates.GetPoupancaPeriodsAsync(appliedAt, today);

        var factor = 1m;
        var cursor = appliedAt;
        foreach (var period in periods.OrderBy(p => p.Start))
        {
            if (period.Start != cursor) continue;
            if (period.End > today) break; // período corrente ainda não completou o aniversário
            factor *= 1 + period.Rate / 100m;
            cursor = period.End;
        }
        return inv.PrincipalAmount * factor;
    }

    // Tabela regressiva de IOF (Decreto 6.306/2007, Anexo) — % do rendimento tributado por dia corrido
    // decorrido, do dia 1 (96%) ao dia 29 (3%); a partir do dia 30 a alíquota é 0%. Não se aplica à poupança.
    private static readonly int[] IofTable =
        [96, 93, 90, 86, 83, 80, 76, 73, 70, 66, 63, 60, 56, 53, 50, 46, 43, 40, 36, 33, 30, 26, 23, 20, 16, 13, 10, 6, 3];

    // IOF regressivo (resgates antes de 30 dias) seguido de IR regressivo sobre o rendimento
    // (isento de IR para LCI/LCA/Poupança — mas LCI/LCA ainda pagam IOF; só a poupança é isenta de ambos).
    private static decimal CalculateNet(Investment inv, decimal gross, DateOnly appliedAt, DateOnly today)
    {
        var yield = gross - inv.PrincipalAmount;
        if (yield <= 0) return gross;

        var days = today.DayNumber - appliedAt.DayNumber;

        if (inv.AssetType != "POUPANCA" && days < 30)
            yield *= 1 - IofTable[days - 1] / 100m;

        var isento = inv.AssetType is "LCI" or "LCA" or "POUPANCA";
        if (!isento)
        {
            var aliquota = days switch
            {
                <= 180 => 0.225m,
                <= 360 => 0.20m,
                <= 720 => 0.175m,
                _ => 0.15m
            };
            yield *= 1 - aliquota;
        }

        return inv.PrincipalAmount + yield;
    }
}
