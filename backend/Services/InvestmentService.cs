using Dapper;
using FinApp.Api.Data;
using FinApp.Api.Models;

namespace FinApp.Api.Services;

public class InvestmentService(DbConnectionFactory db, InvestmentCalculationService calculator)
{
    public async Task<List<InvestmentResponse>> ListAsync(int userId)
    {
        using var conn = db.Create();
        var investments = (await conn.QueryAsync<Investment>(
            "SELECT * FROM investments WHERE user_id = @UserId ORDER BY applied_at DESC",
            new { UserId = userId })).ToList();

        if (investments.Count == 0) return [];

        var ids = investments.Select(i => i.Id).ToList();
        var movements = (await conn.QueryAsync<InvestmentMovement>(
            "SELECT * FROM investment_movements WHERE investment_id IN @Ids",
            new { Ids = ids })).ToList();

        var movementsByInvestment = movements.GroupBy(m => m.InvestmentId).ToDictionary(g => g.Key, g => g.ToList());

        var result = new List<InvestmentResponse>();
        foreach (var inv in investments)
            result.Add(await ToResponseAsync(inv, movementsByInvestment.TryGetValue(inv.Id, out var m) ? m : []));
        return result;
    }

    public async Task<InvestmentSummaryResponse> GetSummaryAsync(int userId)
    {
        var investments = await ListAsync(userId);
        return new InvestmentSummaryResponse(
            investments.Sum(i => i.NetContributed),
            investments.Sum(i => i.GrossValue),
            investments.Sum(i => i.NetValue));
    }

    public async Task<InvestmentResponse> CreateAsync(int userId, CreateInvestmentRequest req)
    {
        using var conn = db.Create();
        var id = await conn.ExecuteScalarAsync<int>(@"
            INSERT INTO investments (user_id, institution, asset_type, indexer, indexer_rate, principal_amount, applied_at, maturity_at)
            VALUES (@UserId, @Institution, @AssetType, @Indexer, @IndexerRate, @PrincipalAmount, @AppliedAt, @MaturityAt);
            SELECT LAST_INSERT_ID();",
            new
            {
                UserId = userId,
                req.Institution,
                req.AssetType,
                req.Indexer,
                req.IndexerRate,
                req.PrincipalAmount,
                AppliedAt = req.AppliedAt,
                MaturityAt = req.MaturityAt
            });

        var inv = await conn.QueryFirstAsync<Investment>("SELECT * FROM investments WHERE id = @Id", new { Id = id });
        return await ToResponseAsync(inv, []);
    }

    public async Task<InvestmentResponse?> UpdateAsync(int id, int userId, UpdateInvestmentRequest req)
    {
        using var conn = db.Create();
        var rows = await conn.ExecuteAsync(@"
            UPDATE investments
            SET institution      = @Institution,
                asset_type       = @AssetType,
                indexer          = @Indexer,
                indexer_rate     = @IndexerRate,
                principal_amount = @PrincipalAmount,
                applied_at       = @AppliedAt,
                maturity_at      = @MaturityAt
            WHERE id = @Id AND user_id = @UserId",
            new
            {
                req.Institution,
                req.AssetType,
                req.Indexer,
                req.IndexerRate,
                req.PrincipalAmount,
                AppliedAt = req.AppliedAt,
                MaturityAt = req.MaturityAt,
                Id = id,
                UserId = userId
            });
        if (rows == 0) return null;

        var inv = await conn.QueryFirstAsync<Investment>("SELECT * FROM investments WHERE id = @Id", new { Id = id });
        var movements = (await conn.QueryAsync<InvestmentMovement>(
            "SELECT * FROM investment_movements WHERE investment_id = @Id", new { Id = id })).ToList();
        return await ToResponseAsync(inv, movements);
    }

    public async Task<bool> DeleteAsync(int id, int userId)
    {
        using var conn = db.Create();
        var rows = await conn.ExecuteAsync(
            "DELETE FROM investments WHERE id = @Id AND user_id = @UserId",
            new { Id = id, UserId = userId });
        return rows > 0;
    }

    /// <summary>Adiciona um aporte/resgate a um ativo existente. Retorna null se o ativo não existe/não é do usuário,
    /// ou uma mensagem de erro se o resgate excede o saldo bruto atual.</summary>
    public async Task<(InvestmentResponse? Result, string? Error)> AddMovementAsync(int investmentId, int userId, CreateMovementRequest req)
    {
        using var conn = db.Create();
        var inv = await conn.QueryFirstOrDefaultAsync<Investment>(
            "SELECT * FROM investments WHERE id = @Id AND user_id = @UserId", new { Id = investmentId, UserId = userId });
        if (inv is null) return (null, null);

        var movements = (await conn.QueryAsync<InvestmentMovement>(
            "SELECT * FROM investment_movements WHERE investment_id = @Id", new { Id = investmentId })).ToList();

        if (req.Type == "RESGATE")
        {
            var (currentGross, _, _) = await calculator.CalculateAsync(inv, movements);
            if (req.Amount > currentGross)
                return (null, $"O resgate (R$ {req.Amount:0.00}) excede o valor bruto atual do ativo (R$ {currentGross:0.00}).");
        }

        await conn.ExecuteAsync(@"
            INSERT INTO investment_movements (investment_id, type, amount, movement_date)
            VALUES (@InvestmentId, @Type, @Amount, @MovementDate)",
            new { InvestmentId = investmentId, req.Type, req.Amount, MovementDate = req.Date });

        movements = (await conn.QueryAsync<InvestmentMovement>(
            "SELECT * FROM investment_movements WHERE investment_id = @Id", new { Id = investmentId })).ToList();
        return (await ToResponseAsync(inv, movements), null);
    }

    private async Task<InvestmentResponse> ToResponseAsync(Investment inv, List<InvestmentMovement> movements)
    {
        var (gross, net, netContributed) = await calculator.CalculateAsync(inv, movements);
        return new InvestmentResponse(
            inv.Id, inv.Institution, inv.AssetType, inv.Indexer, inv.IndexerRate,
            inv.PrincipalAmount, inv.AppliedAt, inv.MaturityAt, gross, net, netContributed,
            movements.OrderBy(m => m.MovementDate).ThenBy(m => m.Id)
                .Select(m => new MovementResponse(m.Id, m.Type, m.Amount, m.MovementDate)).ToList());
    }
}
