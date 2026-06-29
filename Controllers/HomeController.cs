using Microsoft.AspNetCore.Mvc;
using WorshipSearchWeb.Models;
using WorshipSearchWeb.Services;

namespace WorshipSearchWeb.Controllers;

public class HomeController : Controller
{
    private readonly IJsonSearchEngine _searchEngine;
    private readonly ISearchHistoryService _history;

    public HomeController(IJsonSearchEngine searchEngine, ISearchHistoryService history)
    {
        _searchEngine = searchEngine;
        _history = history;
    }

    public async Task<IActionResult> Index(string? q, bool approvedOnly = false)
    {
        var query = new SearchQuery { Query = q ?? string.Empty, ApprovedOnly = approvedOnly };
        var results = await _searchEngine.SearchAsync(query);

        if (!string.IsNullOrWhiteSpace(q))
            await _history.LogSearchAsync(q, results.Count);

        ViewBag.Query = q;
        ViewBag.ApprovedOnly = approvedOnly;
        return View(results);
    }
}
