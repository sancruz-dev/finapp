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
    {
        var result = await svc.ListAsync(UserId);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCategoryRequest req)
    {
        var category = await svc.CreateAsync(UserId, req);
        return StatusCode(201, category);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var ok = await svc.DeleteAsync(id, UserId);
        if (!ok) return NotFound();
        return Ok(new { message = "Removido com sucesso" });
    }
}
