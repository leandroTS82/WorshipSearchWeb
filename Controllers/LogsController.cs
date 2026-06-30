using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace WorshipSearchWeb.Controllers;

// Views folder is "AppLogs" to avoid git's [Ll]ogs/ ignore rule
public class LogsController : Controller
{
    private readonly IWebHostEnvironment _env;

    public LogsController(IWebHostEnvironment env) => _env = env;

    [HttpGet]
    public async Task<IActionResult> Index(string? level = null, string? evt = null, int take = 300)
    {
        var logPath = Path.Combine(_env.ContentRootPath, "logs", "app.jsonl");
        ViewBag.Level = level;
        ViewBag.Event = evt;
        ViewBag.Take  = take;

        if (!System.IO.File.Exists(logPath))
            return View("~/Views/AppLogs/Index.cshtml", Array.Empty<JsonElement>());

        var lines = await System.IO.File.ReadAllLinesAsync(logPath);
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
            .Where(e => string.IsNullOrEmpty(level) ||
                        (e.TryGetProperty("level", out var lv) && lv.GetString() == level))
            .Where(e => string.IsNullOrEmpty(evt) ||
                        (e.TryGetProperty("evt", out var ev) &&
                         (ev.GetString()?.Contains(evt, StringComparison.OrdinalIgnoreCase) ?? false)))
            .Take(take)
            .ToList();

        ViewBag.TotalLines = lines.Length;
        return View("~/Views/AppLogs/Index.cshtml", entries);
    }

    [HttpPost]
    public IActionResult Clear()
    {
        var logPath = Path.Combine(_env.ContentRootPath, "logs", "app.jsonl");
        if (System.IO.File.Exists(logPath))
            System.IO.File.WriteAllText(logPath, "");
        return Json(new { success = true });
    }
}
