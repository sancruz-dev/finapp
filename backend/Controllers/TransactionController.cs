using FinApp.Api.Models;
using FinApp.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinApp.Api.Controllers;

[ApiController]
[Route("api/transactions")]
[Authorize]
public class TransactionController(TransactionService svc) : ControllerBase
{
    private int UserId => int.Parse(User.FindFirst("id")!.Value);

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int? month,
        [FromQuery] int? year,
        [FromQuery] string? type,
        [FromQuery(Name = "category_id")] int? categoryId)
    {
        var result = await svc.ListAsync(UserId, month, year, type, categoryId);
        return Ok(result);
    }

    // ATENÇÃO: /summary deve vir ANTES de /{id} para não ser capturado como ID
    [HttpGet("summary")]
    public async Task<IActionResult> Summary([FromQuery] int month, [FromQuery] int year)
    {
        var result = await svc.SummaryAsync(UserId, month, year);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTransactionRequest req)
    {
        var tx = await svc.CreateAsync(UserId, req);
        return StatusCode(201, tx);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateTransactionRequest req)
    {
        var ok = await svc.UpdateAsync(id, UserId, req);
        if (!ok) return NotFound();
        return Ok(new { message = "Atualizado com sucesso" });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var ok = await svc.DeleteAsync(id, UserId);
        if (!ok) return NotFound();
        return Ok(new { message = "Removido com sucesso" });
    }
}
