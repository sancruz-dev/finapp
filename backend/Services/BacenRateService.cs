using System.Data;
using System.Globalization;
using System.Text.Json;
using Dapper;
using FinApp.Api.Data;

namespace FinApp.Api.Services;

/// <summary>
/// Busca e cacheia as séries históricas do Banco Central (API SGS, pública e gratuita,
/// sem autenticação) usadas para calcular o valor atual dos investimentos.
/// </summary>
public class BacenRateService(HttpClient http, DbConnectionFactory db)
{
    private const int CdiSeries = 12;
    private const int SelicSeries = 11;
    private const int PoupancaSeries = 195; // rendimento da poupança por período de 30 dias (regra vigente)

    private record BacenPoint(string Data, string? DataFim, string Valor);

    /// <summary>Taxas diárias (%) de CDI/Selic entre start e end, inclusive. Usa cache local (indexer_daily_rates) e só busca no Bacen o que faltar.</summary>
    public async Task<List<(DateOnly Date, decimal Rate)>> GetDailyRatesAsync(string indexer, DateOnly start, DateOnly end)
    {
        var seriesCode = indexer switch
        {
            "CDI" => CdiSeries,
            "SELIC" => SelicSeries,
            _ => throw new ArgumentException($"Indexador diário não suportado: {indexer}")
        };

        using var conn = db.Create();
        var cached = await LoadCachedAsync(conn, indexer, start, end);

        var toFetch = new List<(DateOnly Start, DateOnly End)>();
        if (cached.Count == 0)
        {
            toFetch.Add((start, end));
        }
        else
        {
            var minCached = cached.Min(c => c.Date);
            var maxCached = cached.Max(c => c.Date);
            if (start < minCached) toFetch.Add((start, minCached.AddDays(-1)));
            if (end > maxCached) toFetch.Add((maxCached.AddDays(1), end));
        }

        var fetchedAny = false;
        foreach (var (rangeStart, rangeEnd) in toFetch)
        {
            if (rangeStart > rangeEnd) continue;
            foreach (var (date, rate) in await FetchSeriesAsync(seriesCode, rangeStart, rangeEnd))
            {
                await conn.ExecuteAsync(
                    "INSERT INTO indexer_daily_rates (indexer, rate_date, rate) VALUES (@Indexer, @RateDate, @Rate) " +
                    "ON DUPLICATE KEY UPDATE rate = VALUES(rate)",
                    new { Indexer = indexer, RateDate = date.ToDateTime(TimeOnly.MinValue), Rate = rate });
                fetchedAny = true;
            }
        }

        return fetchedAny ? await LoadCachedAsync(conn, indexer, start, end) : cached;
    }

    /// <summary>
    /// Períodos de rendimento da poupança (regra vigente, aniversário mensal) com início entre start e end.
    /// Cada item cobre 30 dias corridos a partir de Start — só deve ser considerado creditado se End já tiver passado.
    /// </summary>
    public async Task<List<(DateOnly Start, DateOnly End, decimal Rate)>> GetPoupancaPeriodsAsync(DateOnly start, DateOnly end)
    {
        var raw = await FetchSeriesRawAsync(PoupancaSeries, start, end);
        return raw
            .Where(p => p.DataFim is not null)
            .Select(p => (ParseDate(p.Data), ParseDate(p.DataFim!), decimal.Parse(p.Valor, CultureInfo.InvariantCulture)))
            .OrderBy(p => p.Item1)
            .ToList();
    }

    private static async Task<List<(DateOnly Date, decimal Rate)>> LoadCachedAsync(IDbConnection conn, string indexer, DateOnly start, DateOnly end)
    {
        var rows = await conn.QueryAsync<(DateTime RateDate, decimal Rate)>(
            "SELECT rate_date, rate FROM indexer_daily_rates WHERE indexer = @Indexer AND rate_date BETWEEN @Start AND @End",
            new { Indexer = indexer, Start = start.ToDateTime(TimeOnly.MinValue), End = end.ToDateTime(TimeOnly.MinValue) });
        return rows.Select(r => (DateOnly.FromDateTime(r.RateDate), r.Rate)).OrderBy(r => r.Item1).ToList();
    }

    private async Task<List<(DateOnly Date, decimal Rate)>> FetchSeriesAsync(int seriesCode, DateOnly start, DateOnly end)
        => (await FetchSeriesRawAsync(seriesCode, start, end))
            .Select(p => (ParseDate(p.Data), decimal.Parse(p.Valor, CultureInfo.InvariantCulture)))
            .ToList();

    private async Task<List<BacenPoint>> FetchSeriesRawAsync(int seriesCode, DateOnly start, DateOnly end)
    {
        var url = $"https://api.bcb.gov.br/dados/serie/bcdata.sgs.{seriesCode}/dados" +
                   $"?formato=json&dataInicial={start:dd/MM/yyyy}&dataFinal={end:dd/MM/yyyy}";

        using var response = await http.GetAsync(url);
        // O Bacen responde 404 (em vez de 200 com lista vazia) quando não há nenhuma taxa publicada
        // no período pedido — comum ao pedir o dia de hoje antes da publicação da taxa.
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return [];
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(json)) return [];

        // O Bacen às vezes responde 200 com um objeto de erro (ou outro formato inesperado)
        // em vez do array de pontos — trata qualquer coisa que não seja array como "sem dados".
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array) return [];

        return JsonSerializer.Deserialize<List<BacenPoint>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
    }

    private static DateOnly ParseDate(string s) => DateOnly.FromDateTime(DateTime.ParseExact(s, "dd/MM/yyyy", CultureInfo.InvariantCulture));
}
