namespace FinApp.Api.Services;

/// <summary>
/// Resolve o período (data inicial/final) usado para filtrar despesas no dashboard.
/// O "dia de fechamento" marca o início de um novo ciclo de fatura: com closingDay=1
/// o período do mês M é [1º dia de M, último dia de M] (comportamento de calendário,
/// igual ao atual). Com closingDay=27, o período do mês M é [dia 27 de M, dia 26 de M+1].
/// </summary>
public static class PeriodCalculator
{
    public static (DateOnly Start, DateOnly End) GetInvoicePeriod(int month, int year, int closingDay)
    {
        var start = ClampedDate(year, month, closingDay);
        var (nextYear, nextMonth) = month == 12 ? (year + 1, 1) : (year, month + 1);
        var end = ClampedDate(nextYear, nextMonth, closingDay).AddDays(-1);
        return (start, end);
    }

    /// <summary>
    /// Receitas (salário, VR/VA) nunca são cortadas pelo período de fatura: sempre
    /// consideram o(s) mês(es) calendário completo(s) que cobrem o período informado.
    /// </summary>
    public static (DateOnly Start, DateOnly End) GetIncomeRange(DateOnly periodStart, DateOnly periodEnd)
    {
        var incomeStart = new DateOnly(periodStart.Year, periodStart.Month, 1);
        var lastDay = DateTime.DaysInMonth(periodEnd.Year, periodEnd.Month);
        var incomeEnd = new DateOnly(periodEnd.Year, periodEnd.Month, lastDay);
        return (incomeStart, incomeEnd);
    }

    private static DateOnly ClampedDate(int year, int month, int day)
    {
        var daysInMonth = DateTime.DaysInMonth(year, month);
        return new DateOnly(year, month, Math.Clamp(day, 1, daysInMonth));
    }
}
