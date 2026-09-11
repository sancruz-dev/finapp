using FinApp.Api.Models;

namespace FinApp.Api.Services;

/// <summary>
/// Calcula o valor bruto e líquido atual de um investimento a partir das taxas históricas
/// do indexador — nada é persistido, o valor é sempre recalculado na leitura. Suporta uma
/// aplicação inicial + qualquer número de aportes/resgates posteriores (cada um datado).
/// </summary>
public class InvestmentCalculationService(BacenRateService rates)
{
    public async Task<(decimal Gross, decimal Net, decimal NetContributed)> CalculateAsync(Investment inv, List<InvestmentMovement> movements)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var timeline = BuildTimeline(inv, movements);

        var gross = await CalculateGrossAsync(inv, timeline, today);
        var (totalAmount, effectiveDayNumber) = CalculateWeightedPosition(timeline, today);
        var net = CalculateNet(inv, gross, totalAmount, effectiveDayNumber, today);
        return (Math.Round(gross, 2), Math.Round(net, 2), Math.Round(totalAmount, 2));
    }

    // Aplicação inicial + cada aporte (positivo)/resgate (negativo), em ordem cronológica —
    // em caso de empate na data, a aplicação inicial vem antes, depois a ordem de criação.
    private static List<(DateOnly Date, decimal Delta)> BuildTimeline(Investment inv, List<InvestmentMovement> movements)
    {
        var events = new List<(DateOnly Date, decimal Delta, int Order)>
        {
            (DateOnly.FromDateTime(inv.AppliedAt), inv.PrincipalAmount, -1)
        };
        events.AddRange(movements.Select(m => (
            DateOnly.FromDateTime(m.MovementDate),
            m.Type == "APORTE" ? m.Amount : -m.Amount,
            m.Id)));

        return events
            .OrderBy(e => e.Date).ThenBy(e => e.Order)
            .Select(e => (e.Date, e.Delta))
            .ToList();
    }

    // Encadeia os períodos de juros compostos entre cada evento — cresce o saldo do evento
    // anterior até a data do próximo, aplica o fluxo (aporte soma, resgate subtrai), repete até hoje.
    private async Task<decimal> CalculateGrossAsync(Investment inv, List<(DateOnly Date, decimal Delta)> timeline, DateOnly today)
    {
        var balance = 0m;
        var cursor = timeline[0].Date;
        foreach (var (date, delta) in timeline)
        {
            balance = await GrowAsync(inv, balance, cursor, date, today);
            balance += delta;
            cursor = date;
        }
        return await GrowAsync(inv, balance, cursor, today, today);
    }

    private async Task<decimal> GrowAsync(Investment inv, decimal balance, DateOnly from, DateOnly to, DateOnly today)
    {
        if (balance <= 0 || from >= to) return balance;
        return inv.Indexer switch
        {
            "CDI" or "SELIC" => await CompoundDailyAsync(inv.Indexer, inv.IndexerRate, balance, from, to, today),
            "PREFIXADO" => CompoundPrefixado(inv.IndexerRate, balance, from, to),
            "POUPANCA" => await CompoundPoupancaAsync(balance, from, to),
            _ => throw new ArgumentException($"Indexador não suportado: {inv.Indexer}")
        };
    }

    // CDI/Selic: juros compostos dia a dia usando a série diária do Bacen × % contratado.
    private async Task<decimal> CompoundDailyAsync(string indexer, decimal? indexerRate, decimal balance, DateOnly from, DateOnly to, DateOnly today)
    {
        var dailyRates = await rates.GetDailyRatesAsync(indexer, from, to);
        var pct = (indexerRate ?? 100m) / 100m;

        var factor = 1m;
        decimal? lastKnownRate = null;
        var hasTo = false;
        foreach (var (date, rate) in dailyRates)
        {
            if (date <= from || date > to) continue;
            factor *= 1 + (rate / 100m) * pct;
            lastKnownRate = rate;
            if (date == to) hasTo = true;
        }

        // O Bacen publica a taxa do dia útil com atraso — projeta o dia corrente com a última taxa
        // conhecida (como os bancos fazem) quando o segmento vai até hoje; o valor se autoajusta
        // quando a taxa oficial sai. Não projeta em fins de semana (sem pregão, sem taxa a publicar).
        var isWeekend = to.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
        if (to == today && !hasTo && !isWeekend && lastKnownRate is not null)
            factor *= 1 + (lastKnownRate.Value / 100m) * pct;

        return balance * factor;
    }

    // Prefixado: sem série externa — juros compostos por dias corridos sobre a taxa anual contratada.
    private static decimal CompoundPrefixado(decimal? indexerRate, decimal balance, DateOnly from, DateOnly to)
    {
        var days = to.DayNumber - from.DayNumber;
        var annualRate = (double)(indexerRate ?? 0m) / 100.0;
        var factor = Math.Pow(1 + annualRate, days / 365.0);
        return balance * (decimal)factor;
    }

    // Poupança: só credita rendimento a cada "aniversário" de 30 dias corridos (regra real da caderneta).
    private async Task<decimal> CompoundPoupancaAsync(decimal balance, DateOnly from, DateOnly to)
    {
        var periods = await rates.GetPoupancaPeriodsAsync(from, to);

        var factor = 1m;
        var cursor = from;
        foreach (var period in periods.OrderBy(p => p.Start))
        {
            if (period.Start != cursor) continue;
            if (period.End > to) break; // período corrente ainda não completou o aniversário
            factor *= 1 + period.Rate / 100m;
            cursor = period.End;
        }
        return balance * factor;
    }

    // "Idade" efetiva do dinheiro hoje, ponderada pelo valor de cada aporte — um resgate reduz a
    // posição proporcionalmente (não escolhe de qual aporte veio), preservando a idade média do
    // que resta. Retorna também o total líquido aportado (custo, não composto) usado como base do
    // rendimento tributável.
    private static (decimal TotalAmount, int EffectiveDayNumber) CalculateWeightedPosition(List<(DateOnly Date, decimal Delta)> timeline, DateOnly today)
    {
        var totalAmount = 0m;
        var weightedDaySum = 0m;
        foreach (var (date, delta) in timeline)
        {
            if (delta > 0)
            {
                weightedDaySum += delta * date.DayNumber;
                totalAmount += delta;
            }
            else if (totalAmount > 0)
            {
                var resgate = Math.Min(-delta, totalAmount);
                var frac = resgate / totalAmount;
                weightedDaySum -= weightedDaySum * frac;
                totalAmount -= resgate;
            }
        }

        var effectiveDayNumber = totalAmount > 0 ? (int)Math.Round(weightedDaySum / totalAmount) : today.DayNumber;
        return (totalAmount, effectiveDayNumber);
    }

    // Tabela regressiva de IOF (Decreto 6.306/2007, Anexo) — % do rendimento tributado por dia corrido
    // decorrido, do dia 1 (96%) ao dia 29 (3%); a partir do dia 30 a alíquota é 0%. Não se aplica à poupança.
    private static readonly int[] IofTable =
        [96, 93, 90, 86, 83, 80, 76, 73, 70, 66, 63, 60, 56, 53, 50, 46, 43, 40, 36, 33, 30, 26, 23, 20, 16, 13, 10, 6, 3];

    // IOF regressivo (resgates antes de 30 dias) seguido de IR regressivo sobre o rendimento
    // (isento de IR para LCI/LCA/Poupança — mas LCI/LCA ainda pagam IOF; só a poupança é isenta de ambos).
    // Com múltiplos aportes/resgates, usa a "idade" efetiva ponderada por valor em vez da data de
    // aplicação original.
    private static decimal CalculateNet(Investment inv, decimal gross, decimal totalAmount, int effectiveDayNumber, DateOnly today)
    {
        var yield = gross - totalAmount;
        if (yield <= 0) return gross;

        var days = Math.Max(0, today.DayNumber - effectiveDayNumber);

        if (inv.AssetType != "POUPANCA" && days is > 0 and < 30)
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

        return totalAmount + yield;
    }
}
