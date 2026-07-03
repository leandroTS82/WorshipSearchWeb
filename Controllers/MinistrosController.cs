using Microsoft.AspNetCore.Mvc;
using WorshipSearchWeb.Services;

namespace WorshipSearchWeb.Controllers;

public class MinistrosController : Controller
{
    private readonly IEscalaRepository _repo;

    public MinistrosController(IEscalaRepository repo)
    {
        _repo = repo;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var list = await _repo.GetMinistrosAsync();
        return Json(list);
    }

    [HttpPost]
    public async Task<IActionResult> Add([FromBody] MinistroRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Nome))
            return BadRequest(new { error = "Nome é obrigatório" });

        await _repo.AddMinistroAsync(req.Nome.Trim());
        var list = await _repo.GetMinistrosAsync();
        return Json(new { success = true, items = list });
    }

    [HttpPost]
    public async Task<IActionResult> Remove([FromBody] MinistroRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Nome))
            return BadRequest(new { error = "Nome é obrigatório" });

        await _repo.RemoveMinistroAsync(req.Nome.Trim());
        var list = await _repo.GetMinistrosAsync();
        return Json(new { success = true, items = list });
    }
}

public class MinistroRequest
{
    public string Nome { get; set; } = string.Empty;
}
