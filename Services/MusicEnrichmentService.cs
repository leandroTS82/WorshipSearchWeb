using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using WorshipSearchWeb.Models;
using WorshipSearchWeb.Prompts;

namespace WorshipSearchWeb.Services;

public interface IMusicEnrichmentService
{
    Task<(SearchMetadata Tags, string KeyUsed)> EnrichParagraphAsync(string paragraph, string title, string artist, SearchMetadata existing, string? key = null);
    Task<(string Summary, string Explanation, string PracticalApplication, string WorshipStyle, string EnergyLevel, string Occasion, List<string> WorshipMoments, string KeyUsed)> EnrichSummaryAsync(MusicDocument doc, string? key = null);
}

public class MusicEnrichmentService : IMusicEnrichmentService
{
    private readonly IGroqService _groqService;
    private readonly IPromptBuilder _promptBuilder;
    private readonly ILogger<MusicEnrichmentService> _logger;
    private readonly IAppLogger _appLogger;

    public MusicEnrichmentService(IGroqService groqService, IPromptBuilder promptBuilder,
        ILogger<MusicEnrichmentService> logger, IAppLogger appLogger)
    {
        _groqService = groqService;
        _promptBuilder = promptBuilder;
        _logger = logger;
        _appLogger = appLogger;
    }

    public async Task<(SearchMetadata Tags, string KeyUsed)> EnrichParagraphAsync(string paragraph, string title, string artist, SearchMetadata existing, string? key = null)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var prompt = _promptBuilder.BuildParagraphPrompt(paragraph, title, artist, existing);
            var (response, keyUsed) = await _groqService.CompleteAsync(prompt, providedKey: key);
            var result = ParseTagsOnly(response);
            await _appLogger.InfoAsync("enrich_paragraph", new
            {
                song        = $"{title} — {artist}",
                elapsed_ms  = sw.ElapsedMilliseconds,
                themes      = result.Themes.Count,
                moods       = result.Moods.Count,
                keywords    = result.Keywords.Count,
                biblical    = result.BiblicalReferences.Count,
                worship_mom = result.WorshipMoments.Count,
                ok          = true
            });
            return (result, keyUsed);
        }
        catch (Exception ex)
        {
            await _appLogger.ErrorAsync("enrich_paragraph_fail", ex, new { song = $"{title} — {artist}", elapsed_ms = sw.ElapsedMilliseconds });
            throw;
        }
    }

    public async Task<(string Summary, string Explanation, string PracticalApplication, string WorshipStyle, string EnergyLevel, string Occasion, List<string> WorshipMoments, string KeyUsed)> EnrichSummaryAsync(MusicDocument doc, string? key = null)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var prompt = _promptBuilder.BuildSummaryPrompt(doc);
            var (response, keyUsed) = await _groqService.CompleteAsync(prompt, providedKey: key);

            var cleaned = StripMarkdown(response);
            // Extract JSON object — LLM may add prose before/after the braces
            var start = cleaned.IndexOf('{');
            var end   = cleaned.LastIndexOf('}');
            if (start < 0 || end <= start)
            {
                await _appLogger.WarnAsync("enrich_summary_no_json", new { song = $"{doc.Title} — {doc.Artist}", elapsed_ms = sw.ElapsedMilliseconds, preview = cleaned[..Math.Min(120, cleaned.Length)] });
                return (string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, new(), keyUsed);
            }

            var node = JsonNode.Parse(cleaned[start..(end + 1)]);
            if (node == null)
            {
                await _appLogger.WarnAsync("enrich_summary_parse_null", new { song = $"{doc.Title} — {doc.Artist}", elapsed_ms = sw.ElapsedMilliseconds });
                return (string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, new(), keyUsed);
            }

            var summary       = node["summary"]?.GetValue<string>() ?? string.Empty;
            var explanation   = node["explanation"]?.GetValue<string>() ?? string.Empty;
            var practical     = node["practical_application"]?.GetValue<string>() ?? string.Empty;
            var worshipStyle  = node["worship_style"]?.GetValue<string>() ?? string.Empty;
            var energyLevel   = node["energy_level"]?.GetValue<string>() ?? string.Empty;
            var occasion      = node["occasion"]?.GetValue<string>() ?? string.Empty;
            var moments       = ParseArray(node["worship_moments"]);

            await _appLogger.InfoAsync("enrich_summary", new
            {
                song            = $"{doc.Title} — {doc.Artist}",
                elapsed_ms      = sw.ElapsedMilliseconds,
                has_summary     = !string.IsNullOrEmpty(summary),
                worship_style   = worshipStyle,
                energy_level    = energyLevel,
                worship_moments = moments.Count,
                ok              = true
            });
            return (summary, explanation, practical, worshipStyle, energyLevel, occasion, moments, keyUsed);
        }
        catch (Exception ex)
        {
            await _appLogger.ErrorAsync("enrich_summary_fail", ex, new { song = $"{doc.Title} — {doc.Artist}", elapsed_ms = sw.ElapsedMilliseconds });
            throw;
        }
    }

    private static string StripMarkdown(string response) =>
        Regex.Replace(response, @"```(?:json)?", string.Empty).Trim();

    private static SearchMetadata ParseTagsOnly(string llmResponse)
    {
        var result = new SearchMetadata();
        try
        {
            var cleaned = StripMarkdown(llmResponse);
            var node = JsonNode.Parse(cleaned);
            if (node == null) return result;

            result.Themes = ParseArray(node["themes"]);
            result.Moods = ParseArray(node["moods"]);
            result.Contexts = ParseArray(node["contexts"]);
            result.Keywords = ParseArray(node["keywords"]);
            result.BiblicalTopics = ParseArray(node["biblical_topics"]);
            result.BiblicalReferences = ParseArray(node["biblical_references"]);
            result.BiblicalBooks = ParseArray(node["biblical_books"]);
            result.BiblicalCharacters = ParseArray(node["biblical_characters"]);
            result.Synonyms = ParseArray(node["synonyms"]);
            result.WorshipStyle = node["worship_style"]?.GetValue<string>() ?? string.Empty;
            result.EnergyLevel = node["energy_level"]?.GetValue<string>() ?? string.Empty;
            result.Occasion = node["occasion"]?.GetValue<string>() ?? string.Empty;
            result.WorshipMoments = ParseArray(node["worship_moments"]);
        }
        catch { /* return empty on parse failure */ }
        return result;
    }

    private static List<string> ParseArray(JsonNode? node)
    {
        if (node is not JsonArray arr) return new();
        return arr
            .Select(n => n?.GetValue<string>() ?? string.Empty)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
    }
}
