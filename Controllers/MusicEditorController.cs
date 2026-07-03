using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using WorshipSearchWeb.Models;
using WorshipSearchWeb.Services;

namespace WorshipSearchWeb.Controllers;

public class MusicEditorController : Controller
{
    private readonly IMusicRepository _repository;
    private readonly ISuggestionService _suggestions;

    public MusicEditorController(IMusicRepository repository, ISuggestionService suggestions)
    {
        _repository = repository;
        _suggestions = suggestions;
    }

    [HttpGet]
    public async Task<IActionResult> Load(string fileName)
    {
        var doc = await _repository.LoadAsync(fileName);
        if (doc == null) return NotFound();
        return Json(doc);
    }

    [HttpGet]
    public async Task<IActionResult> SongView(string fileName, string? back = null)
    {
        var doc = await _repository.LoadAsync(fileName);
        if (doc == null) return NotFound();
        ViewBag.FileName = fileName;
        ViewBag.BackUrl  = back ?? Url.Action("Index", "Home");
        return View(doc);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string fileName)
    {
        var doc = await _repository.LoadAsync(fileName);
        if (doc == null) return NotFound();

        ViewBag.FileName = fileName;
        return View(doc);
    }

    [HttpPost]
    public async Task<IActionResult> Save([FromBody] MusicDocument doc, [FromQuery] string fileName)
    {
        try
        {
            if (doc.Metadata != null)
            {
                // If YoutubeId looks like a URL, extract the 11-char ID
                var raw = doc.Metadata.YoutubeId;
                if (!string.IsNullOrWhiteSpace(raw) && raw.Contains('/'))
                {
                    var m = Regex.Match(raw, @"[?&]v=([A-Za-z0-9_-]{11})")
                         ?? Regex.Match(raw, @"youtu\.be/([A-Za-z0-9_-]{11})");
                    doc.Metadata.YoutubeId = m.Success ? m.Groups[1].Value : raw;
                }
            }

            await _repository.SaveAsync(doc);
            _suggestions.Invalidate();
            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
