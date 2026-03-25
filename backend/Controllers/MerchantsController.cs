using FinApp.Api.Models;
using FinApp.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinApp.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/merchants")]
public class MerchantsController(MerchantNormalizerService merchantService) : ControllerBase
{
    private int UserId => int.Parse(User.FindFirst("id")!.Value);

    // ── Merchants canônicos ──────────────────────────────────────────────

    /// Lista todos os comerciantes do usuário
    [HttpGet]
    public async Task<IActionResult> List()
        => Ok(await merchantService.ListMerchantsAsync(UserId));

    /// Cria um comerciante canônico manualmente
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateMerchantRequest req)
        => Ok(await merchantService.CreateMerchantAsync(req, UserId));

    /// Adiciona um alias (nome bruto) a um comerciante existente
    [HttpPost("{id}/aliases")]
    public async Task<IActionResult> AddAlias(int id, [FromBody] AddAliasRequest req)
    {
        await merchantService.AddAliasAsync(id, req.RawName, UserId);
        return Ok(new { message = "Alias adicionado e modelo retreinado." });
    }

    // ── Fila de revisão ──────────────────────────────────────────────────

    /// Lista itens pendentes de revisão
    [HttpGet("review-queue")]
    public async Task<IActionResult> GetReviewQueue()
        => Ok(await merchantService.GetReviewQueueAsync(UserId));

    /// Resolve (confirma/corrige) um item da fila
    [HttpPost("review-queue/resolve")]
    public async Task<IActionResult> Resolve([FromBody] ResolveReviewRequest req)
    {
        await merchantService.ResolveReviewAsync(req, UserId);
        return Ok(new { message = "Resolvido. Modelo será retreinado." });
    }

    // ── Debug / utilidades ───────────────────────────────────────────────

    /// Testa a predição sem persistir nada
    [HttpGet("predict")]
    public async Task<IActionResult> Predict([FromQuery] string name)
        => Ok(await merchantService.PredictAsync(name, UserId));

    /// Força retreino manual do modelo
    [HttpPost("retrain")]
    public async Task<IActionResult> Retrain()
    {
        await merchantService.TrainModelAsync(UserId);
        return Ok(new { message = "Modelo retreinado." });
    }
}

// DTO adicional
public class AddAliasRequest
{
    public string RawName { get; set; } = "";
}