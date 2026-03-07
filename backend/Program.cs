using System.Text;
using System.Text.Json;
using DotNetEnv;
using FinApp.Api.Data;
using FinApp.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

Env.Load();

// ── Dapper: mapear snake_case do MySQL para PascalCase do C#
Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

var builder = WebApplication.CreateBuilder(args);

var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET") ?? "changeme_super_secret_key_here";

// ── Serviços ───────────────────────────────────────────────────────────────
builder.Services.AddSingleton<DbConnectionFactory>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<TransactionService>();
builder.Services.AddScoped<CategoryService>();
builder.Services.AddScoped<ImportService>();
builder.Services.AddScoped<FinancialContextPlugin>();  // novo
builder.Services.AddScoped<AiService>();               // novo
builder.Services.AddSingleton<JwtService>();

// ── JSON em snake_case ─────────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(opt =>
    {
        opt.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
        opt.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower;
        opt.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });

builder.Services.AddEndpointsApiExplorer();

// ── CORS ───────────────────────────────────────────────────────────────────
builder.Services.AddCors(opt =>
    opt.AddDefaultPolicy(p =>
        p.SetIsOriginAllowed(_ => true)
         .AllowAnyHeader()
         .AllowAnyMethod()));

// ── JWT ────────────────────────────────────────────────────────────────────
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();
