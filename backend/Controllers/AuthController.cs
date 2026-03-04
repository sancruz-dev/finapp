using FinApp.Api.Models;
using FinApp.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace FinApp.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AuthService authService, JwtService jwtService) : ControllerBase
{
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var (user, error) = await authService.LoginAsync(req.Email, req.Password);
        if (user is null) return Unauthorized(new { error });

        var token = jwtService.Generate(user);
        return Ok(new { token, user = new { user.Id, user.Name, user.Email } });
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        var (ok, error) = await authService.RegisterAsync(req.Name, req.Email, req.Password);
        if (!ok) return Conflict(new { error });
        return StatusCode(201, new { message = "Usuário criado com sucesso" });
    }
}
