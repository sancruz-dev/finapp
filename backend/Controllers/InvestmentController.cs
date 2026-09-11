using FinApp.Api.Models;
using FinApp.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinApp.Api.Controllers;

[ApiController]
[Route("api/investments")]
[Authorize]
public class InvestmentController(InvestmentService svc) : ControllerBase
{
    private int UserId => int.Parse(User.FindFirst("id")!.Value);

    [HttpGet]
    public async Task<IActionResult> List()
        => Ok(await svc.ListAsync(UserId));

    [HttpGet("summary")]
    public async Task<IActionResult> Summary()
        => Ok(await svc.GetSummaryAsync(UserId));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateInvestmentRequest req)
        => StatusCode(201, await svc.CreateAsync(UserId, req));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateInvestmentRequest req)
    {
        var updated = await svc.UpdateAsync(id, UserId, req);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
        => await svc.DeleteAsync(id, UserId) ? Ok(new { message = "Removido" }) : NotFound();
}
