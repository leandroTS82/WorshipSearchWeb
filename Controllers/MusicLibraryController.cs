using Microsoft.AspNetCore.Mvc;
using WorshipSearchWeb.Models;
using WorshipSearchWeb.Services;

namespace WorshipSearchWeb.Controllers;

public record SetStatusRequest(string FileName, string Status);

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

    public async Task<IActionResult> Index(string? q, int page = 1, string? status = null)
    {
        const int pageSize = 50;
        page = Math.Max(1, page);
        var query = new SearchQuery { Query = q ?? string.Empty, Page = page, PageSize = pageSize, StatusFilter = status };
        var results = await _searchEngine.SearchAsync(query);
        var total   = await _searchEngine.CountAsync(new SearchQuery { Query = q ?? string.Empty, StatusFilter = status });
        ViewBag.Query        = q;
        ViewBag.StatusFilter = status;
        ViewBag.CurrentPage  = page;
        ViewBag.PageSize     = pageSize;
        ViewBag.TotalCount   = total;
        ViewBag.TotalPages   = (int)Math.Ceiling((double)total / pageSize);
        return View(results);
    }

    [HttpPost]
    public async Task<IActionResult> SetStatus([FromBody] SetStatusRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.FileName) ||
            (req.Status != "approved" && req.Status != "review"))
            return BadRequest(new { error = "invalid" });

        var docs = await _repository.LoadAllAsync();
        var entry = docs.FirstOrDefault(x => x.FileName == req.FileName);
        if (entry.Doc == null) return NotFound(new { error = "not found" });

        entry.Doc.Status = req.Status;
        await _repository.SaveAsync(entry.Doc);
        return Json(new { success = true, status = req.Status });
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
        var approved = all.Count(d => d.Doc.IsApproved);
        return Json(new { total, approved });
    }
}
