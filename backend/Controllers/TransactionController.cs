using FinApp.Api.Models;
using FinApp.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinApp.Api.Controllers;

[ApiController]
[Route("api/transactions")]
[Authorize]
public class TransactionController(TransactionService svc, UserService userSvc) : ControllerBase
{
    private int UserId => int.Parse(User.FindFirst("id")!.Value);

    /// <summary>
    /// Resolve o período a filtrar: se startDate/endDate vierem explícitos (filtro livre),
    /// usa-os direto. Caso contrário, se month/year vierem, calcula o período automático
    /// a partir do dia de fechamento configurado pelo usuário. Sem nenhum dos dois, retorna
    /// null (sem filtro de período).
    /// </summary>
    private async Task<(DateOnly? Start, DateOnly? End)?> ResolvePeriodAsync(
        int? month, int? year, string? startDate, string? endDate)
    {
        if (!string.IsNullOrEmpty(startDate) && !string.IsNullOrEmpty(endDate))
        {
            if (!DateOnly.TryParse(startDate, out var s) || !DateOnly.TryParse(endDate, out var e))
                return null;
            return (s, e);
        }

        if (month.HasValue && year.HasValue)
        {
            var user = await userSvc.GetByIdAsync(UserId);
            var closingDay = user?.ClosingDay ?? 1;
            var (start, end) = PeriodCalculator.GetInvoicePeriod(month.Value, year.Value, closingDay);
            return (start, end);
        }

        return (null, null);
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int? month,
        [FromQuery] int? year,
        [FromQuery(Name = "start_date")] string? startDate,
        [FromQuery(Name = "end_date")] string? endDate,
        [FromQuery] string? type,
        [FromQuery(Name = "category_id")] int? categoryId)
    {
        var period = await ResolvePeriodAsync(month, year, startDate, endDate);
        if (period is null) return BadRequest(new { error = "Período inválido" });

        var result = await svc.ListAsync(UserId, period.Value.Start, period.Value.End, type, categoryId);
        return Ok(result);
    }

    // ATENÇÃO: /summary deve vir ANTES de /{id} para não ser capturado como ID
    [HttpGet("summary")]
    public async Task<IActionResult> Summary(
        [FromQuery] int? month,
        [FromQuery] int? year,
        [FromQuery(Name = "start_date")] string? startDate,
        [FromQuery(Name = "end_date")] string? endDate)
    {
        var period = await ResolvePeriodAsync(month, year, startDate, endDate);
        if (period is null || period.Value.Start is null || period.Value.End is null)
            return BadRequest(new { error = "Informe month/year ou start_date/end_date" });

        var result = await svc.SummaryAsync(UserId, period.Value.Start.Value, period.Value.End.Value);
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
