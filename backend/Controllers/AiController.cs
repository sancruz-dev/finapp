using FinApp.Api.Models;
using FinApp.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinApp.Api.Controllers;

[ApiController]
[Route("api/ai")]
[Authorize]
public class AiController(AiService aiService, FinancialContextPlugin contextPlugin) : ControllerBase
{
    private int UserId => int.Parse(User.FindFirst("id")!.Value);
    private string UserName => User.FindFirst("name")?.Value ?? "Usuário";

    // ── Chats ──────────────────────────────────────────────────────────────

    [HttpGet("chats")]
    public async Task<IActionResult> ListChats()
        => Ok(await aiService.ListChatsAsync(UserId));

    [HttpPost("chats")]
    public async Task<IActionResult> CreateChat([FromBody] CreateAiChatRequest req)
    {
        var chat = await aiService.CreateChatAsync(UserId, req.Title);
        return StatusCode(201, chat);
    }

    [HttpDelete("chats/{id:int}")]
    public async Task<IActionResult> DeleteChat(int id)
        => await aiService.DeleteChatAsync(id, UserId)
            ? Ok(new { message = "Conversa removida." })
            : NotFound();

    // ── Messages ───────────────────────────────────────────────────────────

    [HttpGet("chats/{id:int}/messages")]
    public async Task<IActionResult> GetMessages(int id)
        => Ok(await aiService.GetMessagesAsync(id, UserId));

    [HttpPost("chats/{id:int}/messages")]
    public async Task<IActionResult> SendMessage(int id, [FromBody] SendAiMessageRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Content))
            return BadRequest(new { error = "Mensagem não pode ser vazia." });

        try
        {
            var response = await aiService.SendMessageAsync(id, UserId, UserName, req.Content);
            return Ok(response);
        }
        catch (UnauthorizedAccessException)
        {
            return NotFound(new { error = "Conversa não encontrada." });
        }
    }

    // ── Debug ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Retorna o system prompt exato e o contexto bruto que seriam enviados ao modelo.
    /// Use para verificar se os dados financeiros estão corretos antes de culpar o modelo.
    /// </summary>
    [HttpGet("debug/context")]
    public async Task<IActionResult> DebugContext()
    {
        var ctx = await contextPlugin.BuildContextAsync(UserId, UserName);
        var prompt = FinancialContextPlugin.BuildSystemPrompt(ctx);
        return Ok(new
        {
            system_prompt = prompt,
            raw_context = ctx,
            generated_at = DateTime.Now,
        });
    }
}