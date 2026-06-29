using Microsoft.AspNetCore.Mvc;
using WorshipSearchWeb.Services;

namespace WorshipSearchWeb.Controllers;

public class MusicSearchController : Controller
{
    private readonly IMusicSearchService _musicSearchService;
    private readonly ISearchRestrictionsService _restrictions;

    public MusicSearchController(IMusicSearchService musicSearchService, ISearchRestrictionsService restrictions)
    {
        _musicSearchService = musicSearchService;
        _restrictions = restrictions;
    }

    public IActionResult Index()
    {
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Results(string q)
    {
        if (string.IsNullOrWhiteSpace(q))
            return RedirectToAction(nameof(Index));

        var results = await _musicSearchService.SearchAsync(q);
        ViewBag.Query = q;
        return View(results);
    }

    [HttpGet]
    public async Task<IActionResult> SearchApi(string q)
    {
        if (string.IsNullOrWhiteSpace(q))
            return Json(new List<object>());

        var results = await _musicSearchService.SearchAsync(q);

        var restr = await _restrictions.LoadAsync();
        if (restr.Excluded.Count > 0)
        {
            var excluded = restr.Excluded.Select(e => e.ToLowerInvariant()).ToHashSet();
            results = results.Where(r =>
                !excluded.Any(ex =>
                    (r.Genre ?? string.Empty).ToLowerInvariant().Contains(ex)
                )).ToList();
        }

        return Json(results.Select(r => new
        {
            r.Title,
            r.Artist,
            r.Genre,
            r.ArtistSlug,
            r.SongSlug,
            r.Image,
            r.Popularity,
            r.Url
        }));
    }
}
