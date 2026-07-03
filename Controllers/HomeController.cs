using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using WorshipSearchWeb.Models;
using WorshipSearchWeb.Services;

namespace WorshipSearchWeb.Controllers;

public class HomeController : Controller
{
    private readonly IJsonSearchEngine _searchEngine;
    private readonly IMusicRepository _repository;
    private readonly ISearchHistoryService _history;
    private readonly IQueryExpansionService _expansion;
    private readonly ISearchIndexClassifier _classifier;
    private readonly ISearchDocumentService _searchDocService;
    private readonly ISuggestionService _suggestions;
    private readonly IPopularityService _popularity;
    private readonly IAdditionalSuggestionsService _additionalSuggestions;
    private readonly ILogger<HomeController> _logger;

    public HomeController(
        IJsonSearchEngine searchEngine,
        IMusicRepository repository,
        ISearchHistoryService history,
        IQueryExpansionService expansion,
        ISearchIndexClassifier classifier,
        ISearchDocumentService searchDocService,
        ISuggestionService suggestions,
        IPopularityService popularity,
        IAdditionalSuggestionsService additionalSuggestions,
        ILogger<HomeController> logger)
    {
        _searchEngine          = searchEngine;
        _repository            = repository;
        _history               = history;
        _expansion             = expansion;
        _classifier            = classifier;
        _searchDocService      = searchDocService;
        _suggestions           = suggestions;
        _popularity            = popularity;
        _additionalSuggestions = additionalSuggestions;
        _logger                = logger;
    }

    public async Task<IActionResult> Index(string? q)
    {
        var rawCriteria = string.IsNullOrWhiteSpace(q)
            ? Array.Empty<string>()
            : q.Split(';', StringSplitOptions.RemoveEmptyEntries)
               .Select(c => c.Trim())
               .Where(c => c.Length > 0)
               .ToArray();

        List<LibrarySearchResult> results;
        List<SearchCriterion>? classifiedCriteria = null;

        if (rawCriteria.Length > 1)
        {
            // Multi-criteria: classify all in parallel (one LLM key each), then typed matching
            var classifyTasks = rawCriteria.Select(c => _classifier.ClassifyAsync(c));
            classifiedCriteria = (await Task.WhenAll(classifyTasks)).ToList();

            results = await _searchEngine.SearchByDocumentAsync(classifiedCriteria);

            // Persist search document without blocking the response
            _ = SaveSearchDocumentAsync(q!, classifiedCriteria, results.Count);
            if (results.Count > 0)
                _ = TrackPopularityAsync(results);
        }
        else
        {
            // Single criterion or empty: fast path, no LLM upfront
            var query = new SearchQuery { Query = q ?? string.Empty };
            results = await _searchEngine.SearchAsync(query);

            // Fallback: LLM expansion when nothing found
            if (results.Count == 0 && !string.IsNullOrWhiteSpace(q))
            {
                var (expandedQ, expandedDescription) = await _expansion.ExpandAsync(q);
                if (!string.IsNullOrWhiteSpace(expandedQ))
                {
                    var expandedQuery = new SearchQuery { Query = expandedQ };
                    var expandedResults = await _searchEngine.SearchAsync(expandedQuery);
                    if (expandedResults.Count > 0)
                    {
                        results = expandedResults;
                        ViewBag.LlmExpanded = true;
                        ViewBag.LlmExpandedDescription = expandedDescription;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                try { await _history.LogSearchAsync(q, results.Count); }
                catch (Exception ex) { _logger.LogWarning(ex, "Failed to log search history"); }

                if (results.Count > 0)
                    _ = TrackPopularityAsync(results);
            }
        }

        // When no query, load top songs ordered by search popularity then enrichment richness
        if (rawCriteria.Length == 0)
        {
            try
            {
                var all = await _repository.LoadAllAsync();
                var popularityCounts = await _popularity.LoadAsync();
                var top = all
                    .Select(x => new
                    {
                        x.FileName,
                        x.Doc,
                        Popularity = popularityCounts.GetValueOrDefault(x.FileName),
                        Richness = (x.Doc.Search?.Themes.Count ?? 0)
                                 + (x.Doc.Search?.BiblicalReferences.Count ?? 0)
                                 + (x.Doc.Search?.Moods.Count ?? 0)
                                 + (x.Doc.Search?.Keywords.Count ?? 0)
                                 + (string.IsNullOrEmpty(x.Doc.Summary) ? 0 : 2)
                    })
                    .OrderByDescending(x => x.Popularity)
                    .ThenByDescending(x => x.Richness)
                    .Take(6)
                    .Select(x => new LibrarySearchResult { Document = x.Doc, FileName = x.FileName, Score = x.Popularity })
                    .ToList();
                ViewBag.TopSongs   = top;
                ViewBag.TotalCount = all.Count;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load top songs");
            }
        }

        var addlSuggestions = await _additionalSuggestions.LoadAsync();
        ViewBag.Query                 = q;
        ViewBag.CriteriaCount         = rawCriteria.Length;
        ViewBag.ClassifiedCriteria    = classifiedCriteria;
        ViewBag.Suggestions           = await _suggestions.GetSuggestionsAsync();
        ViewBag.AdditionalSuggestions = addlSuggestions.Items.ToArray();
        return View(results);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        var feature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
        _logger.LogError(feature?.Error, "Unhandled exception at {Path}", feature?.Path);
        return View(new ErrorViewModel { RequestId = HttpContext.TraceIdentifier });
    }

    private async Task TrackPopularityAsync(List<LibrarySearchResult> results)
    {
        try { await _popularity.IncrementAsync(results.Select(r => r.FileName)); }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to track popularity"); }
    }

    private async Task SaveSearchDocumentAsync(string rawQuery, List<SearchCriterion> criteria, int resultCount)
    {
        try
        {
            var doc = new SearchDocument
            {
                Id          = Guid.NewGuid().ToString("N")[..12],
                SearchedAt  = DateTime.UtcNow.ToString("o"),
                RawQuery    = rawQuery,
                Criteria    = criteria,
                ResultCount = resultCount
            };
            await _searchDocService.SaveAsync(doc);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save search document");
        }
    }
}
