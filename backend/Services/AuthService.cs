using Dapper;
using FinApp.Api.Data;
using FinApp.Api.Models;

namespace FinApp.Api.Services;

public class AuthService(DbConnectionFactory db)
{
    private static readonly string[][] DefaultCategories =
    [
        ["Salário",     "income",  "#22c55e"],
        ["Freelance",   "income",  "#3b82f6"],
        ["Alimentação", "expense", "#f97316"],
        ["Transporte",  "expense", "#8b5cf6"],
        ["Moradia",     "expense", "#ef4444"],
        ["Lazer",       "expense", "#ec4899"],
        ["Saúde",       "expense", "#14b8a6"],
    ];

    public async Task<(User? user, string? error)> LoginAsync(string email, string password)
    {
        using var conn = db.Create();
        var user = await conn.QueryFirstOrDefaultAsync<User>(
            "SELECT * FROM users WHERE email = @Email", new { Email = email });

        if (user is null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            return (null, "Email ou senha inválidos");

        return (user, null);
    }

    public async Task<(bool ok, string? error)> RegisterAsync(string name, string email, string password)
    {
        using var conn = db.Create();

        // Verificar e-mail duplicado
        var exists = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM users WHERE email = @Email", new { Email = email });
        if (exists > 0) return (false, "Email já cadastrado");

        var hash = BCrypt.Net.BCrypt.HashPassword(password, 10);
        var userId = await conn.ExecuteScalarAsync<int>(
            "INSERT INTO users (name, email, password_hash) VALUES (@Name, @Email, @Hash); SELECT LAST_INSERT_ID();",
            new { Name = name, Email = email, Hash = hash });

        foreach (var cat in DefaultCategories)
            await conn.ExecuteAsync(
                "INSERT INTO categories (user_id, name, type, color) VALUES (@UserId, @Name, @Type, @Color)",
                new { UserId = userId, Name = cat[0], Type = cat[1], Color = cat[2] });

        return (true, null);
    }
}
