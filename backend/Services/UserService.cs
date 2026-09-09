using Dapper;
using FinApp.Api.Data;
using FinApp.Api.Models;

namespace FinApp.Api.Services;

public class UserService(DbConnectionFactory db)
{
    public async Task<User?> GetByIdAsync(int userId)
    {
        using var conn = db.Create();
        return await conn.QueryFirstOrDefaultAsync<User>(
            "SELECT * FROM users WHERE id = @UserId", new { UserId = userId });
    }

    public async Task<User?> UpdateClosingDayAsync(int userId, int closingDay)
    {
        if (closingDay is < 1 or > 31) return null;

        using var conn = db.Create();
        await conn.ExecuteAsync(
            "UPDATE users SET closing_day = @ClosingDay WHERE id = @UserId",
            new { ClosingDay = closingDay, UserId = userId });

        return await GetByIdAsync(userId);
    }

    public async Task<User?> UpdateNameAsync(int userId, string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;

        using var conn = db.Create();
        await conn.ExecuteAsync(
            "UPDATE users SET name = @Name WHERE id = @UserId",
            new { Name = name.Trim(), UserId = userId });

        return await GetByIdAsync(userId);
    }

    public async Task<(bool ok, string? error)> UpdatePasswordAsync(int userId, string currentPassword, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
            return (false, "A nova senha deve ter pelo menos 6 caracteres");

        var user = await GetByIdAsync(userId);
        if (user is null || !BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
            return (false, "Senha atual incorreta");

        using var conn = db.Create();
        var hash = BCrypt.Net.BCrypt.HashPassword(newPassword, 10);
        await conn.ExecuteAsync(
            "UPDATE users SET password_hash = @Hash WHERE id = @UserId",
            new { Hash = hash, UserId = userId });

        return (true, null);
    }
}
