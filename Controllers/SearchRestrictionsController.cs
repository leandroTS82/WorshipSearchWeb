using Microsoft.AspNetCore.Mvc;
using WorshipSearchWeb.Models;
using WorshipSearchWeb.Services;

namespace WorshipSearchWeb.Controllers;

public class SearchRestrictionsController : Controller
{
    private readonly ISearchRestrictionsService _restrictionsService;

    public SearchRestrictionsController(ISearchRestrictionsService restrictionsService)
    {
        _restrictionsService = restrictionsService;
    }

    public async Task<IActionResult> Index()
    {
        var restrictions = await _restrictionsService.LoadAsync();
        return View(restrictions);
    }

    [HttpPost]
    public async Task<IActionResult> Add([FromBody] AddRestrictionRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Term))
            return BadRequest(new { error = "Term is required" });

        var restrictions = await _restrictionsService.LoadAsync();
        var term = req.Term.Trim();

        if (!restrictions.Excluded.Any(e => e.Equals(term, StringComparison.OrdinalIgnoreCase)))
            restrictions.Excluded.Add(term);

        await _restrictionsService.SaveAsync(restrictions);
        return Json(new { success = true, excluded = restrictions.Excluded });
    }

    [HttpPost]
    public async Task<IActionResult> Remove([FromBody] AddRestrictionRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Term))
            return BadRequest(new { error = "Term is required" });

        var restrictions = await _restrictionsService.LoadAsync();
        restrictions.Excluded.RemoveAll(e => e.Equals(req.Term.Trim(), StringComparison.OrdinalIgnoreCase));
        await _restrictionsService.SaveAsync(restrictions);
        return Json(new { success = true, excluded = restrictions.Excluded });
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var restrictions = await _restrictionsService.LoadAsync();
        return Json(restrictions.Excluded);
    }
}

