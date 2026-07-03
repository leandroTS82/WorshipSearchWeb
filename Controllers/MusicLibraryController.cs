using Microsoft.AspNetCore.Mvc;
using WorshipSearchWeb.Models;
using WorshipSearchWeb.Services;

namespace WorshipSearchWeb.Controllers;

public class MusicLibraryController : Controller
{
    private readonly IMusicRepository _repository;
    private readonly IJsonSearchEngine _searchEngine;
    private readonly ILlmKeyService _llmKeyService;

    public MusicLibraryController(IMusicRepository repository, IJsonSearchEngine searchEngine, ILlmKeyService llmKeyService)
    {
        _repository = repository;
        _searchEngine = searchEngine;
        _llmKeyService = llmKeyService;
    }

    public async Task<IActionResult> Index(string? q, int page = 1)
    {
        const int pageSize = 50;
        page = Math.Max(1, page);
        var query = new SearchQuery { Query = q ?? string.Empty, Page = page, PageSize = pageSize };
        var results = await _searchEngine.SearchAsync(query);
        var total   = await _searchEngine.CountAsync(new SearchQuery { Query = q ?? string.Empty });
        ViewBag.Query        = q;
        ViewBag.CurrentPage  = page;
        ViewBag.PageSize     = pageSize;
        ViewBag.TotalCount   = total;
        ViewBag.TotalPages   = (int)Math.Ceiling((double)total / pageSize);
        return View(results);
    }

    [HttpDelete]
    public async Task<IActionResult> Delete(string fileName)
    {
        await _repository.DeleteAsync(fileName);
        return Json(new { success = true });
    }

    [HttpGet]
    public async Task<IActionResult> GetUnenriched(bool all = false)
    {
        var docs = await _repository.LoadAllAsync();
        var source = all
            ? docs
            : docs.Where(x => string.IsNullOrWhiteSpace(x.Doc.Summary) || (x.Doc.Search?.Themes?.Count ?? 0) < 2);
        var result = source
            .Select(x => new { fileName = x.FileName, title = x.Doc.Title, artist = x.Doc.Artist })
            .ToList();
        return Json(result);
    }

    public IActionResult EnrichAll() => View();

    [HttpGet]
    public async Task<IActionResult> NextKey()
    {
        var key = await _llmKeyService.GetGroqKeyAsync();
        return Json(new { key, masked = GroqService.MaskKey(key) });
    }

    [HttpGet]
    public async Task<IActionResult> Stats()
    {
        var all = await _repository.LoadAllAsync();
        var total = all.Count;
        var approved = all.Count(d => d.Doc.Approved);
        return Json(new { total, approved });
    }
}
