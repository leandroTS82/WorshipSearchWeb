using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace WorshipSearchWeb.Controllers;

// Views folder is "AppLogs" to avoid git's [Ll]ogs/ ignore rule
public class LogsController : Controller
{
    private readonly IWebHostEnvironment _env;

    public LogsController(IWebHostEnvironment env) => _env = env;

    private string LogPath => Path.Combine(_env.ContentRootPath, "logs", "app.jsonl");

    [HttpGet]
    public async Task<IActionResult> Index(string? level = null, string? evt = null, int take = 3)
    {
        ViewBag.Level = level;
        ViewBag.Event = evt;
        ViewBag.Take  = take;

        if (!System.IO.File.Exists(LogPath))
            return View("~/Views/AppLogs/Index.cshtml", Array.Empty<JsonElement>().ToList());

        var lines = await System.IO.File.ReadAllLinesAsync(LogPath);
        var allFiltered = lines
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Reverse()
            .Select(line =>
            {
                try   { return (JsonElement?)JsonSerializer.Deserialize<JsonElement>(line); }
                catch { return null; }
            })
            .Where(e => e.HasValue)
            .Select(e => e!.Value)
            .Where(e => string.IsNullOrEmpty(level) ||
                        (e.TryGetProperty("level", out var lv) && lv.GetString() == level))
            .Where(e => string.IsNullOrEmpty(evt) ||
                        (e.TryGetProperty("evt", out var ev) &&
                         (ev.GetString()?.Contains(evt, StringComparison.OrdinalIgnoreCase) ?? false)))
            .ToList();

        ViewBag.TotalLines   = allFiltered.Count;
        ViewBag.ShowingAll   = take >= allFiltered.Count || take <= 0;
        return View("~/Views/AppLogs/Index.cshtml", take > 0 ? allFiltered.Take(take).ToList() : allFiltered);
    }

    [HttpGet]
    public async Task<IActionResult> Recent()
    {
        if (!System.IO.File.Exists(LogPath))
            return Json(Array.Empty<object>());

        var lines = await System.IO.File.ReadAllLinesAsync(LogPath);
        var entries = lines
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Reverse()
            .Select(line =>
            {
                try   { return (JsonElement?)JsonSerializer.Deserialize<JsonElement>(line); }
                catch { return null; }
            })
            .Where(e => e.HasValue)
            .Select(e => e!.Value)
            .Take(3)
            .ToList();

        return Json(entries);
    }

    [HttpGet]
    public IActionResult Download()
    {
        if (!System.IO.File.Exists(LogPath))
            return NotFound("Arquivo de log não encontrado.");

        var bytes = System.IO.File.ReadAllBytes(LogPath);
        return File(bytes, "text/plain", $"app-{DateTime.Now:yyyyMMdd-HHmm}.log");
    }

    [HttpPost]
    public IActionResult Clear()
    {
        if (System.IO.File.Exists(LogPath))
            System.IO.File.WriteAllText(LogPath, "");
        return Json(new { success = true });
    }
}
