using Microsoft.AspNetCore.Mvc;
using WorshipSearchWeb.Models;
using WorshipSearchWeb.Services;

namespace WorshipSearchWeb.Controllers;

public class MusicIndexController : Controller
{
    private readonly IMusicHtmlParser _parser;
    private readonly IMusicJsonGenerator _generator;
    private readonly IMusicEnrichmentService _enrichment;
    private readonly IMusicRepository _repository;
    private readonly ISuggestionService _suggestions;
    private readonly ILogger<MusicIndexController> _logger;

    public MusicIndexController(
        IMusicHtmlParser parser,
        IMusicJsonGenerator generator,
        IMusicEnrichmentService enrichment,
        IMusicRepository repository,
        ISuggestionService suggestions,
        ILogger<MusicIndexController> logger)
    {
        _parser = parser;
        _generator = generator;
        _enrichment = enrichment;
        _repository = repository;
        _suggestions = suggestions;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Preview(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return BadRequest("URL is required");

        try
        {
            var parsed = await _parser.ParseAsync(url);
            var doc = _generator.Generate(parsed);
            ViewBag.AlreadyExists = _repository.Exists(_repository.GetFileName(doc));
            return View(doc);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse {Url}", url);
            TempData["Error"] = $"Failed to fetch/parse URL: {ex.Message}";
            return RedirectToAction("Index", "MusicSearch");
        }
    }

    [HttpPost]
    public async Task<IActionResult> EnrichParagraph([FromBody] EnrichParagraphRequest req)
    {
        try
        {
            var newTags = await _enrichment.EnrichParagraphAsync(
                req.Paragraph, req.Title, req.Artist, req.ExistingTags);
            return Json(newTags);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Paragraph enrichment failed");
            return IsRateLimit(ex)
                ? StatusCode(429, new { error = ex.Message })
                : StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> EnrichSummary([FromBody] MusicDocument doc)
    {
        try
        {
            var (summary, explanation, practical, worshipStyle, energyLevel, occasion, worshipMoments) = await _enrichment.EnrichSummaryAsync(doc);
            return Json(new
            {
                summary,
                explanation,
                practical_application = practical,
                worship_style = worshipStyle,
                energy_level = energyLevel,
                occasion,
                worship_moments = worshipMoments
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Summary enrichment failed");
            return IsRateLimit(ex)
                ? StatusCode(429, new { error = ex.Message })
                : StatusCode(500, new { error = ex.Message });
        }
    }

    private static bool IsRateLimit(Exception ex) =>
        ex.Message.Contains("TooManyRequests", StringComparison.OrdinalIgnoreCase) ||
        ex.Message.Contains("rate_limit", StringComparison.OrdinalIgnoreCase) ||
        ex.Message.Contains("429", StringComparison.Ordinal);

    [HttpPost]
    public async Task<IActionResult> Save([FromBody] MusicDocument doc)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(doc.Metadata.IndexedAt))
                doc.Metadata.IndexedAt = DateTime.UtcNow.ToString("O");

            await _repository.SaveAsync(doc);
            _suggestions.Invalidate();
            return Json(new { success = true, fileName = _repository.GetFileName(doc) });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Save failed");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
