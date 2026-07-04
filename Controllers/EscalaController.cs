using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Text;
using WorshipSearchWeb.Models;
using WorshipSearchWeb.Services;

namespace WorshipSearchWeb.Controllers;

public class EscalaController : Controller
{
    private readonly IEscalaRepository _escala;
    private readonly IMusicRepository  _musicRepo;

    public EscalaController(IEscalaRepository escala, IMusicRepository musicRepo)
    {
        _escala    = escala;
        _musicRepo = musicRepo;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? view = null)
    {
        var docs = await _escala.LoadAllAsync();
        if (!string.IsNullOrEmpty(view))
            ViewBag.AutoViewId = view;
        return View(docs);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string? id = null)
    {
        EscalaDocument? doc = null;
        if (!string.IsNullOrEmpty(id))
            doc = await _escala.LoadAsync(id);

        ViewBag.IsNew = doc == null;
        return View(doc ?? new EscalaDocument
        {
            Data = DateOnly.FromDateTime(DateTime.Now).ToString("yyyy-MM-dd")
        });
    }

    [HttpPost]
    public async Task<IActionResult> Save([FromBody] EscalaDocument doc)
    {
        await _escala.SaveAsync(doc);

        if (!string.IsNullOrWhiteSpace(doc.Ministro))
            await _escala.AddMinistroAsync(doc.Ministro.Trim());

        // Persist new YouTube URLs back to the library document
        foreach (var m in (doc.Musicas ?? new()).Where(
            m => !string.IsNullOrEmpty(m.FileName) && !string.IsNullOrEmpty(m.YoutubeUrl)))
        {
            var libDoc = await _musicRepo.LoadAsync(m.FileName);
            if (libDoc != null)
            {
                libDoc.Metadata ??= new MusicMetadata();
                // Only overwrite if the user provided a real URL (not one we auto-built from the ID)
                if (string.IsNullOrEmpty(libDoc.Metadata.YoutubeUrl))
                {
                    libDoc.Metadata.YoutubeUrl = m.YoutubeUrl;
                    await _musicRepo.SaveAsync(libDoc);
                }
            }
        }

        return Json(new { success = true, id = doc.Id });
    }

    [HttpPost]
    public async Task<IActionResult> Delete(string id)
    {
        await _escala.DeleteAsync(id);
        return Json(new { success = true });
    }

    [HttpGet]
    public async Task<IActionResult> Get(string id)
    {
        var doc = await _escala.LoadAsync(id);
        if (doc == null) return NotFound();
        return Json(doc);
    }

    [HttpGet]
    public async Task<IActionResult> CopyText(string id)
    {
        var doc = await _escala.LoadAsync(id);
        if (doc == null) return NotFound();
        return Content(BuildCopyText(doc), "text/plain");
    }

    [HttpGet]
    public async Task<IActionResult> SearchSongs(string q)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
            return Json(Array.Empty<object>());

        var normQ = NormSimple(q);
        var all   = await _musicRepo.LoadAllAsync();

        var results = all
            .Where(x =>
                NormSimple(x.Doc.Title  ?? "").Contains(normQ) ||
                NormSimple(x.Doc.Artist ?? "").Contains(normQ))
            .OrderBy(x => x.Doc.Title)
            .Take(10)
            .Select(x => new
            {
                fileName   = x.FileName,
                titulo     = x.Doc.Title,
                artista    = x.Doc.Artist,
                youtubeUrl = x.Doc.Metadata?.YoutubeUrl ?? string.Empty,
                youtubeId  = x.Doc.Metadata?.YoutubeId  ?? string.Empty
            });

        return Json(results);
    }

    [HttpGet]
    public async Task<IActionResult> Ministros()
    {
        return Json(await _escala.GetMinistrosAsync());
    }

    [HttpGet]
    public async Task<IActionResult> Lyrics(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return Json(new { lyrics = string.Empty });
        var doc = await _musicRepo.LoadAsync(fileName);
        return Json(new { lyrics = doc?.Lyrics ?? string.Empty });
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static string NormSimple(string s)
    {
        var formed = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(formed.Length);
        foreach (var c in formed)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        return sb.ToString().ToLowerInvariant();
    }

    internal static string BuildCopyText(EscalaDocument doc)
    {
        var lines = new List<string>();
        var obs = (doc.Observacao ?? "").Trim();
        if (!string.IsNullOrEmpty(obs)) { lines.Add(obs); lines.Add(string.Empty); }

        if (!string.IsNullOrEmpty(doc.Data) && DateOnly.TryParse(doc.Data, out var date))
        {
            var dayNames = new[] { "Domingo", "Segunda-feira", "Terça-feira", "Quarta-feira",
                                   "Quinta-feira", "Sexta-feira", "Sábado" };
            var dow = dayNames[(int)date.DayOfWeek];
            var titulo = (doc.Titulo ?? "").ToLowerInvariant();
            lines.Add($"{dow} dia {date:dd/MM}{(string.IsNullOrEmpty(titulo) ? "" : " " + titulo)}");
            lines.Add(string.Empty);
        }

        string? currentMomento = null;
        foreach (var m in doc.Musicas ?? new())
        {
            var mom = (m.Momento ?? "").Trim();
            if (!string.IsNullOrEmpty(mom) && mom != currentMomento)
            {
                if (currentMomento != null) lines.Add(string.Empty);
                lines.Add(mom + ":");
                currentMomento = mom;
            }
            var songLine = m.Titulo + " - " + m.Artista;
            if (!string.IsNullOrEmpty(m.Obs)) songLine += $" ({m.Obs})";
            var hasUrl = !string.IsNullOrEmpty(m.YoutubeUrl);
            lines.Add(hasUrl ? songLine + ":" : songLine);
            if (hasUrl) lines.Add(m.YoutubeUrl!);
            lines.Add(string.Empty);
        }

        return string.Join("\n", lines).TrimEnd();
    }
}
