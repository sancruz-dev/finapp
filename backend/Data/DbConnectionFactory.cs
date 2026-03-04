using MySql.Data.MySqlClient;
using System.Data;

namespace FinApp.Api.Data;

public class DbConnectionFactory
{
    private readonly string _connectionString;

    public DbConnectionFactory()
    {
        var host     = Environment.GetEnvironmentVariable("DB_HOST")     ?? "localhost";
        var port     = Environment.GetEnvironmentVariable("DB_PORT")     ?? "3306";
        var user     = Environment.GetEnvironmentVariable("DB_USER")     ?? "root";
        var password = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "";
        var database = Environment.GetEnvironmentVariable("DB_NAME")     ?? "finapp";

        _connectionString = $"Server={host};Port={port};Database={database};Uid={user};Pwd={password};CharSet=utf8mb4;";
    }

    public IDbConnection Create() => new MySqlConnection(_connectionString);
}
