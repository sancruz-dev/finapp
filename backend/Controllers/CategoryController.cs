using FinApp.Api.Models;
using FinApp.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinApp.Api.Controllers;

[ApiController]
[Route("api/categories")]
[Authorize]
public class CategoryController(CategoryService svc) : ControllerBase
{
    private int UserId => int.Parse(User.FindFirst("id")!.Value);

    [HttpGet]
    public async Task<IActionResult> List()
        => Ok(await svc.ListAsync(UserId));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCategoryRequest req)
        => StatusCode(201, await svc.CreateAsync(UserId, req));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
        => await svc.DeleteAsync(id, UserId) ? Ok(new { message = "Removido" }) : NotFound();

    // ── Keywords ───────────────────────────────────────────────────────────

    [HttpPost("{categoryId:int}/keywords")]
    public async Task<IActionResult> AddKeyword(int categoryId, [FromBody] AddKeywordRequest req)
    {
        var kw = await svc.AddKeywordAsync(categoryId, UserId, req.Keyword);
        return kw is null ? NotFound() : StatusCode(201, kw);
    }

    [HttpDelete("keywords/{keywordId:int}")]
    public async Task<IActionResult> DeleteKeyword(int keywordId)
        => await svc.DeleteKeywordAsync(keywordId, UserId) ? Ok(new { message = "Removido" }) : NotFound();
}