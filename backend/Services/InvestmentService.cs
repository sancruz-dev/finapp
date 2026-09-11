using Dapper;
using FinApp.Api.Data;
using FinApp.Api.Models;

namespace FinApp.Api.Services;

public class InvestmentService(DbConnectionFactory db, InvestmentCalculationService calculator)
{
    public async Task<List<InvestmentResponse>> ListAsync(int userId)
    {
        using var conn = db.Create();
        var investments = await conn.QueryAsync<Investment>(
            "SELECT * FROM investments WHERE user_id = @UserId ORDER BY applied_at DESC",
            new { UserId = userId });

        var result = new List<InvestmentResponse>();
        foreach (var inv in investments)
            result.Add(await ToResponseAsync(inv));
        return result;
    }

    public async Task<InvestmentSummaryResponse> GetSummaryAsync(int userId)
    {
        var investments = await ListAsync(userId);
        return new InvestmentSummaryResponse(
            investments.Sum(i => i.PrincipalAmount),
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
        return await ToResponseAsync(inv);
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
        return await ToResponseAsync(inv);
    }

    public async Task<bool> DeleteAsync(int id, int userId)
    {
        using var conn = db.Create();
        var rows = await conn.ExecuteAsync(
            "DELETE FROM investments WHERE id = @Id AND user_id = @UserId",
            new { Id = id, UserId = userId });
        return rows > 0;
    }

    private async Task<InvestmentResponse> ToResponseAsync(Investment inv)
    {
        var (gross, net) = await calculator.CalculateAsync(inv);
        return new InvestmentResponse(
            inv.Id, inv.Institution, inv.AssetType, inv.Indexer, inv.IndexerRate,
            inv.PrincipalAmount, inv.AppliedAt, inv.MaturityAt, gross, net);
    }
}
