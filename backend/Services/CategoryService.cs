using Dapper;
using FinApp.Api.Data;
using FinApp.Api.Models;

namespace FinApp.Api.Services;

public class CategoryService(DbConnectionFactory db)
{
    // ── Categories ─────────────────────────────────────────────────────────

    public async Task<IEnumerable<Category>> ListAsync(int userId)
    {
        using var conn = db.Create();

        var categories = (await conn.QueryAsync<Category>(
            "SELECT * FROM categories WHERE user_id = @UserId ORDER BY type, name",
            new { UserId = userId })).ToList();

        if (categories.Count == 0) return categories;

        var ids = categories.Select(c => c.Id).ToList();
        var keywords = await conn.QueryAsync<CategoryKeyword>(
            "SELECT * FROM category_keywords WHERE category_id IN @Ids",
            new { Ids = ids });

        var kwMap = keywords.GroupBy(k => k.CategoryId)
                            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var cat in categories)
            cat.Keywords = kwMap.TryGetValue(cat.Id, out var kw) ? kw : [];

        return categories;
    }

    public async Task<Category> CreateAsync(int userId, CreateCategoryRequest req)
    {
        using var conn = db.Create();
        var id = await conn.ExecuteScalarAsync<int>(@"
            INSERT INTO categories (user_id, name, type, color, icon)
            VALUES (@UserId, @Name, @Type, @Color, @Icon);
            SELECT LAST_INSERT_ID();",
            new
            {
                UserId = userId,
                req.Name,
                req.Type,
                Color = req.Color ?? "#6366f1",
                Icon = req.Icon
            });

        return new Category
        {
            Id = id,
            UserId = userId,
            Name = req.Name,
            Type = req.Type,
            Color = req.Color ?? "#6366f1",
            Icon = req.Icon
        };
    }

    public async Task<bool> DeleteAsync(int id, int userId)
    {
        using var conn = db.Create();
        var rows = await conn.ExecuteAsync(
            "DELETE FROM categories WHERE id = @Id AND user_id = @UserId",
            new { Id = id, UserId = userId });
        return rows > 0;
    }

    // ── Keywords ───────────────────────────────────────────────────────────

    public async Task<CategoryKeyword?> AddKeywordAsync(int categoryId, int userId, string keyword)
    {
        using var conn = db.Create();

        // Verifica que a categoria pertence ao usuário
        var owner = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM categories WHERE id = @Id AND user_id = @UserId",
            new { Id = categoryId, UserId = userId });
        if (owner == 0) return null;

        var normalised = keyword.Trim().ToUpperInvariant();

        try
        {
            var id = await conn.ExecuteScalarAsync<int>(@"
                INSERT INTO category_keywords (category_id, keyword)
                VALUES (@CategoryId, @Keyword);
                SELECT LAST_INSERT_ID();",
                new { CategoryId = categoryId, Keyword = normalised });

            return new CategoryKeyword { Id = id, CategoryId = categoryId, Keyword = normalised };
        }
        catch   // UNIQUE constraint — keyword já existe
        {
            return await conn.QueryFirstOrDefaultAsync<CategoryKeyword>(
                "SELECT * FROM category_keywords WHERE category_id = @CategoryId AND keyword = @Keyword",
                new { CategoryId = categoryId, Keyword = normalised });
        }
    }

    public async Task<bool> DeleteKeywordAsync(int keywordId, int userId)
    {
        using var conn = db.Create();
        // garante que a keyword pertence a uma categoria do usuário
        var rows = await conn.ExecuteAsync(@"
            DELETE ck FROM category_keywords ck
            JOIN categories c ON ck.category_id = c.id
            WHERE ck.id = @Id AND c.user_id = @UserId",
            new { Id = keywordId, UserId = userId });
        return rows > 0;
    }

    // ── Matching helper (usado pelo ImportService) ─────────────────────────

    public async Task<List<(Category cat, List<string> keywords)>> GetAllWithKeywordsAsync(int userId)
    {
        using var conn = db.Create();

        var categories = (await conn.QueryAsync<Category>(
            "SELECT * FROM categories WHERE user_id = @UserId",
            new { UserId = userId })).ToList();

        var keywords = (await conn.QueryAsync<CategoryKeyword>(
            "SELECT * FROM category_keywords WHERE category_id IN @Ids",
            new { Ids = categories.Select(c => c.Id).ToList() })).ToList();

        return categories
            .Select(cat => (cat, keywords
                .Where(k => k.CategoryId == cat.Id)
                .Select(k => k.Keyword)
                .ToList()))
            .ToList();
    }
}