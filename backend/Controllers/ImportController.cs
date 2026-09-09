using FinApp.Api.Models;
using FinApp.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinApp.Api.Controllers;

[ApiController]
[Route("api/import")]
[Authorize]
public class ImportController(ImportService svc) : ControllerBase
{
    private int UserId => int.Parse(User.FindFirst("id")!.Value);

    /// <summary>Recebe o CSV, faz parse e devolve preview com categorização automática.</summary>
    [HttpPost("preview")]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10 MB
    public async Task<IActionResult> Preview(IFormFile file, [FromForm] string? importType)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "Arquivo inválido ou vazio." });

        var validExtensions = new[] { ".csv", ".xlsx", ".xls" };
        var ext = Path.GetExtension(file.FileName);
        if (!validExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
            return BadRequest(new { error = "Formato não suportado. Envie um arquivo .csv ou .xlsx." });

        var validTypes = new[] { "fatura", "extrato", "vr" };
        var type = validTypes.Contains(importType) ? importType! : "fatura";

        using var stream = file.OpenReadStream();
        var preview = await svc.ParseAndMatchAsync(UserId, stream, file.FileName, type);
        return Ok(preview);
    }

    /// <summary>Confirma e salva as linhas revisadas pelo usuário.</summary>
    [HttpPost("confirm")]
    public async Task<IActionResult> Confirm([FromBody] ImportConfirmRequest req)
    {
        if (req.Rows is null || req.Rows.Count == 0)
            return BadRequest(new { error = "Nenhuma transação para importar." });

        // Checa se já existe uma transação parcelada com mesmo nome + total de parcelas
        // (ex.: reimportação da mesma fatura). O frontend decide: manter e ajustar, ou
        // remover as existentes e reenviar com force=true.
        if (!req.Force)
        {
            var duplicates = await svc.FindInstallmentDuplicatesAsync(UserId, req.Rows);
            if (duplicates.Count > 0)
                return Conflict(new { duplicates });
        }

        var saved = await svc.ConfirmImportAsync(UserId, req.Rows);
        return Ok(new { saved, message = $"{saved} transações importadas com sucesso." });
    }
}