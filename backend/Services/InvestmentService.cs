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

    /// <summary>Edita um aporte/resgate existente. Retorna null se o ativo/movimentação não existe/não é do usuário,
    /// ou uma mensagem de erro se a alteração deixaria o saldo do ativo negativo.</summary>
    public async Task<(InvestmentResponse? Result, string? Error)> UpdateMovementAsync(int investmentId, int movementId, int userId, CreateMovementRequest req)
    {
        using var conn = db.Create();
        var inv = await conn.QueryFirstOrDefaultAsync<Investment>(
            "SELECT * FROM investments WHERE id = @Id AND user_id = @UserId", new { Id = investmentId, UserId = userId });
        if (inv is null) return (null, null);

        var movements = (await conn.QueryAsync<InvestmentMovement>(
            "SELECT * FROM investment_movements WHERE investment_id = @Id", new { Id = investmentId })).ToList();
        var target = movements.FirstOrDefault(m => m.Id == movementId);
        if (target is null) return (null, null);

        var candidate = movements
            .Select(m => m.Id == movementId
                ? new InvestmentMovement { Id = m.Id, InvestmentId = investmentId, Type = req.Type, Amount = req.Amount, MovementDate = DateTime.Parse(req.Date) }
                : m)
            .ToList();

        var (candidateGross, _, _) = await calculator.CalculateAsync(inv, candidate);
        if (candidateGross < 0)
            return (null, "Essa alteração deixaria o saldo do ativo negativo.");

        await conn.ExecuteAsync(@"
            UPDATE investment_movements SET type = @Type, amount = @Amount, movement_date = @MovementDate
            WHERE id = @Id AND investment_id = @InvestmentId",
            new { req.Type, req.Amount, MovementDate = req.Date, Id = movementId, InvestmentId = investmentId });

        movements = (await conn.QueryAsync<InvestmentMovement>(
            "SELECT * FROM investment_movements WHERE investment_id = @Id", new { Id = investmentId })).ToList();
        return (await ToResponseAsync(inv, movements), null);
    }

    /// <summary>Remove um aporte/resgate existente. Retorna (false, null) se o ativo/movimentação não existe/não é do
    /// usuário, ou (false, mensagem) se a remoção deixaria o saldo do ativo negativo.</summary>
    public async Task<(bool Success, string? Error)> DeleteMovementAsync(int investmentId, int movementId, int userId)
    {
        using var conn = db.Create();
        var inv = await conn.QueryFirstOrDefaultAsync<Investment>(
            "SELECT * FROM investments WHERE id = @Id AND user_id = @UserId", new { Id = investmentId, UserId = userId });
        if (inv is null) return (false, null);

        var movements = (await conn.QueryAsync<InvestmentMovement>(
            "SELECT * FROM investment_movements WHERE investment_id = @Id", new { Id = investmentId })).ToList();
        if (!movements.Any(m => m.Id == movementId)) return (false, null);

        var candidate = movements.Where(m => m.Id != movementId).ToList();
        var (candidateGross, _, _) = await calculator.CalculateAsync(inv, candidate);
        if (candidateGross < 0)
            return (false, "Remover essa movimentação deixaria o saldo do ativo negativo.");

        await conn.ExecuteAsync(
            "DELETE FROM investment_movements WHERE id = @Id AND investment_id = @InvestmentId",
            new { Id = movementId, InvestmentId = investmentId });
        return (true, null);
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
