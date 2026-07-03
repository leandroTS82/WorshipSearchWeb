using Microsoft.AspNetCore.Mvc;
using WorshipSearchWeb.Models;
using WorshipSearchWeb.Services;

namespace WorshipSearchWeb.Controllers;

public class AdditionalSuggestionsController : Controller
{
    private readonly IAdditionalSuggestionsService _service;

    public AdditionalSuggestionsController(IAdditionalSuggestionsService service)
    {
        _service = service;
    }

    public async Task<IActionResult> Index()
    {
        var suggestions = await _service.LoadAsync();
        return View(suggestions);
    }

    [HttpPost]
    public async Task<IActionResult> Add([FromBody] AddSuggestionRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Term))
            return BadRequest(new { error = "Term is required" });

        var suggestions = await _service.LoadAsync();
        var term = req.Term.Trim().ToLowerInvariant();

        if (!suggestions.Items.Any(e => e.Equals(term, StringComparison.OrdinalIgnoreCase)))
            suggestions.Items.Add(term);

        await _service.SaveAsync(suggestions);
        return Json(new { success = true, items = suggestions.Items });
    }

    [HttpPost]
    public async Task<IActionResult> Remove([FromBody] AddSuggestionRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Term))
            return BadRequest(new { error = "Term is required" });

        var suggestions = await _service.LoadAsync();
        suggestions.Items.RemoveAll(e => e.Equals(req.Term.Trim(), StringComparison.OrdinalIgnoreCase));
        await _service.SaveAsync(suggestions);
        return Json(new { success = true, items = suggestions.Items });
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var suggestions = await _service.LoadAsync();
        return Json(suggestions.Items);
    }
}

public class AddSuggestionRequest
{
    public string Term { get; set; } = string.Empty;
}
