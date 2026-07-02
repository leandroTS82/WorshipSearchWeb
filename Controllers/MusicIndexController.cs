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
            var (tags, keyUsed) = await _enrichment.EnrichParagraphAsync(
                req.Paragraph, req.Title, req.Artist, req.ExistingTags, req.Key);

            return Json(new
            {
                themes               = tags.Themes,
                moods                = tags.Moods,
                contexts             = tags.Contexts,
                keywords             = tags.Keywords,
                biblical_topics      = tags.BiblicalTopics,
                biblical_references  = tags.BiblicalReferences,
                biblical_books       = tags.BiblicalBooks,
                biblical_characters  = tags.BiblicalCharacters,
                synonyms             = tags.Synonyms,
                worship_moments      = tags.WorshipMoments,
                worship_style        = tags.WorshipStyle,
                energy_level         = tags.EnergyLevel,
                occasion             = tags.Occasion,
                key_used             = GroqService.MaskKey(keyUsed)
            });
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
    public async Task<IActionResult> EnrichSummary([FromBody] MusicDocument doc, [FromQuery] string? key = null)
    {
        try
        {
            var (summary, explanation, practical, worshipStyle, energyLevel, occasion, worshipMoments, keyUsed) = await _enrichment.EnrichSummaryAsync(doc, key);
            return Json(new
            {
                summary,
                explanation,
                practical_application = practical,
                worship_style  = worshipStyle,
                energy_level   = energyLevel,
                occasion,
                worship_moments = worshipMoments,
                key_used = GroqService.MaskKey(keyUsed)
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

            NormalizeTagsToLowercase(doc);
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

    private static void NormalizeTagsToLowercase(MusicDocument doc)
    {
        var s = doc.Search;
        if (s == null) return;
        static List<string> Lower(List<string>? list) =>
            list?.Select(x => x.ToLowerInvariant()).ToList() ?? [];
        s.Themes             = Lower(s.Themes);
        s.Contexts           = Lower(s.Contexts);
        s.Keywords           = Lower(s.Keywords);
        s.BiblicalTopics     = Lower(s.BiblicalTopics);
        s.BiblicalReferences = Lower(s.BiblicalReferences);
        s.BiblicalBooks      = Lower(s.BiblicalBooks);
        s.BiblicalCharacters = Lower(s.BiblicalCharacters);
        s.Moods              = Lower(s.Moods);
        s.Synonyms           = Lower(s.Synonyms);
        s.WorshipMoments     = Lower(s.WorshipMoments);
        if (s.WorshipStyle   != null) s.WorshipStyle   = s.WorshipStyle.ToLowerInvariant();
        if (s.EnergyLevel    != null) s.EnergyLevel    = s.EnergyLevel.ToLowerInvariant();
        if (s.Occasion       != null) s.Occasion       = s.Occasion.ToLowerInvariant();
    }
}
