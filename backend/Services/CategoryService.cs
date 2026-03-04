using Dapper;
using FinApp.Api.Data;
using FinApp.Api.Models;

namespace FinApp.Api.Services;

public class CategoryService(DbConnectionFactory db)
{
    public async Task<IEnumerable<Category>> ListAsync(int userId)
    {
        using var conn = db.Create();
        return await conn.QueryAsync<Category>(
            "SELECT * FROM categories WHERE user_id=@UserId ORDER BY type, name",
            new { UserId = userId });
    }

    public async Task<Category> CreateAsync(int userId, CreateCategoryRequest req)
    {
        using var conn = db.Create();
        var id = await conn.ExecuteScalarAsync<int>(@"
            INSERT INTO categories (user_id, name, type, color, icon) VALUES (@UserId, @Name, @Type, @Color, @Icon);
            SELECT LAST_INSERT_ID();",
            new { UserId = userId, req.Name, req.Type, Color = req.Color ?? "#6366f1", Icon = req.Icon });

        return new Category { Id = id, UserId = userId, Name = req.Name, Type = req.Type,
                              Color = req.Color ?? "#6366f1", Icon = req.Icon };
    }

    public async Task<bool> DeleteAsync(int id, int userId)
    {
        using var conn = db.Create();
        var rows = await conn.ExecuteAsync(
            "DELETE FROM categories WHERE id=@Id AND user_id=@UserId",
            new { Id = id, UserId = userId });
        return rows > 0;
    }
}
