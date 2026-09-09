using FinApp.Api.Models;
using FinApp.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinApp.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UserController(UserService svc) : ControllerBase
{
    private int UserId => int.Parse(User.FindFirst("id")!.Value);

    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var user = await svc.GetByIdAsync(UserId);
        if (user is null) return NotFound();
        return Ok(new { user.Id, user.Name, user.Email, user.ClosingDay });
    }

    [HttpPatch("me/closing-day")]
    public async Task<IActionResult> UpdateClosingDay([FromBody] UpdateClosingDayRequest req)
    {
        var user = await svc.UpdateClosingDayAsync(UserId, req.ClosingDay);
        if (user is null) return BadRequest(new { error = "Dia de fechamento inválido (1-31)" });
        return Ok(new { user.Id, user.Name, user.Email, user.ClosingDay });
    }

    [HttpPatch("me/profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest req)
    {
        var user = await svc.UpdateNameAsync(UserId, req.Name);
        if (user is null) return BadRequest(new { error = "Nome inválido" });
        return Ok(new { user.Id, user.Name, user.Email, user.ClosingDay });
    }

    [HttpPatch("me/password")]
    public async Task<IActionResult> UpdatePassword([FromBody] UpdatePasswordRequest req)
    {
        var (ok, error) = await svc.UpdatePasswordAsync(UserId, req.CurrentPassword, req.NewPassword);
        if (!ok) return BadRequest(new { error });
        return Ok(new { message = "Senha atualizada com sucesso" });
    }
}
