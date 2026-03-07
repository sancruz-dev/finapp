using FinApp.Api.Models;
using FinApp.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinApp.Api.Controllers;

[ApiController]
[Route("api/ai")]
[Authorize]
public class AiController(AiService aiService) : ControllerBase
{
    private int UserId => int.Parse(User.FindFirst("id")!.Value);
    private string UserName => User.FindFirst("name")?.Value ?? "Usuário";

    // ── Chats ──────────────────────────────────────────────────────────────

    /// <summary>Lista todas as conversas do usuário.</summary>
    [HttpGet("chats")]
    public async Task<IActionResult> ListChats()
        => Ok(await aiService.ListChatsAsync(UserId));

    /// <summary>Cria uma nova conversa.</summary>
    [HttpPost("chats")]
    public async Task<IActionResult> CreateChat([FromBody] CreateAiChatRequest req)
    {
        var chat = await aiService.CreateChatAsync(UserId, req.Title);
        return StatusCode(201, chat);
    }

    /// <summary>Deleta uma conversa e todas as suas mensagens.</summary>
    [HttpDelete("chats/{id:int}")]
    public async Task<IActionResult> DeleteChat(int id)
        => await aiService.DeleteChatAsync(id, UserId)
            ? Ok(new { message = "Conversa removida." })
            : NotFound();

    // ── Messages ───────────────────────────────────────────────────────────

    /// <summary>Carrega o histórico de mensagens de uma conversa.</summary>
    [HttpGet("chats/{id:int}/messages")]
    public async Task<IActionResult> GetMessages(int id)
        => Ok(await aiService.GetMessagesAsync(id, UserId));

    /// <summary>Envia uma mensagem e recebe a resposta do assistente.</summary>
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
}
