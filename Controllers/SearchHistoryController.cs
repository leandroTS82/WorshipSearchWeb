using Microsoft.AspNetCore.Mvc;
using WorshipSearchWeb.Models;
using WorshipSearchWeb.Services;

namespace WorshipSearchWeb.Controllers;

public class SearchHistoryController : Controller
{
    private readonly ISearchHistoryService _history;

    public SearchHistoryController(ISearchHistoryService history)
    {
        _history = history;
    }

    public async Task<IActionResult> Index()
    {
        var entries = await _history.LoadRecentAsync(300);
        return View(entries);
    }

    [HttpGet]
    public async Task<IActionResult> Data()
    {
        var entries = await _history.LoadRecentAsync(5);
        return Json(entries);
    }

    [HttpPost]
    public async Task<IActionResult> Log([FromBody] LogSearchRequest req)
    {
        await _history.LogSearchAsync(req.Query, req.ResultCount);
        return Json(new { ok = true });
    }

    [HttpPost]
    public async Task<IActionResult> LogSelection([FromBody] LogSelectionRequest req)
    {
        await _history.LogSelectionAsync(req.Query, req.Title, req.Artist, req.PickedManually);
        return Json(new { ok = true });
    }

    [HttpPost]
    public async Task<IActionResult> Clear()
    {
        await _history.ClearAsync();
        return Json(new { ok = true });
    }
}
