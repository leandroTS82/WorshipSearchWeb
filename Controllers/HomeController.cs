using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using WorshipSearchWeb.Models;
using WorshipSearchWeb.Services;

namespace WorshipSearchWeb.Controllers;

public class HomeController : Controller
{
    private readonly IJsonSearchEngine _searchEngine;
    private readonly ISearchHistoryService _history;
    private readonly IQueryExpansionService _expansion;
    private readonly ILogger<HomeController> _logger;

    public HomeController(
        IJsonSearchEngine searchEngine,
        ISearchHistoryService history,
        IQueryExpansionService expansion,
        ILogger<HomeController> logger)
    {
        _searchEngine = searchEngine;
        _history = history;
        _expansion = expansion;
        _logger = logger;
    }

    public async Task<IActionResult> Index(string? q, bool approvedOnly = false)
    {
        var query = new SearchQuery { Query = q ?? string.Empty, ApprovedOnly = approvedOnly };
        var results = await _searchEngine.SearchAsync(query);

        // Fallback: se não encontrou nada, expande via LLM e re-busca
        if (results.Count == 0 && !string.IsNullOrWhiteSpace(q))
        {
            var expandedQ = await _expansion.ExpandAsync(q);
            if (!string.IsNullOrWhiteSpace(expandedQ))
            {
                var expandedQuery = new SearchQuery { Query = expandedQ, ApprovedOnly = approvedOnly };
                var expandedResults = await _searchEngine.SearchAsync(expandedQuery);
                if (expandedResults.Count > 0)
                {
                    results = expandedResults;
                    ViewBag.LlmExpanded = true;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            try { await _history.LogSearchAsync(q, results.Count); }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to log search history"); }
        }

        ViewBag.Query = q;
        ViewBag.ApprovedOnly = approvedOnly;
        return View(results);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        var feature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
        _logger.LogError(feature?.Error, "Unhandled exception at {Path}", feature?.Path);
        return View(new ErrorViewModel { RequestId = HttpContext.TraceIdentifier });
    }
}
