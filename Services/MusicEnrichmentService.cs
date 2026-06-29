using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using WorshipSearchWeb.Models;
using WorshipSearchWeb.Prompts;

namespace WorshipSearchWeb.Services;

public interface IMusicEnrichmentService
{
    Task<SearchMetadata> EnrichParagraphAsync(string paragraph, string title, string artist, SearchMetadata existing);
    Task<(string Summary, string Explanation, string PracticalApplication)> EnrichSummaryAsync(MusicDocument doc);
}

public class MusicEnrichmentService : IMusicEnrichmentService
{
    private readonly IGroqService _groqService;
    private readonly IPromptBuilder _promptBuilder;
    private readonly ILogger<MusicEnrichmentService> _logger;

    public MusicEnrichmentService(IGroqService groqService, IPromptBuilder promptBuilder, ILogger<MusicEnrichmentService> logger)
    {
        _groqService = groqService;
        _promptBuilder = promptBuilder;
        _logger = logger;
    }

    public async Task<SearchMetadata> EnrichParagraphAsync(string paragraph, string title, string artist, SearchMetadata existing)
    {
        var prompt = _promptBuilder.BuildParagraphPrompt(paragraph, title, artist, existing);
        var response = await _groqService.CompleteAsync(prompt);
        return ParseTagsOnly(response);
    }

    public async Task<(string Summary, string Explanation, string PracticalApplication)> EnrichSummaryAsync(MusicDocument doc)
    {
        var prompt = _promptBuilder.BuildSummaryPrompt(doc);
        var response = await _groqService.CompleteAsync(prompt);

        var cleaned = StripMarkdown(response);
        var node = JsonNode.Parse(cleaned);
        if (node == null) return (string.Empty, string.Empty, string.Empty);

        return (
            node["summary"]?.GetValue<string>() ?? string.Empty,
            node["explanation"]?.GetValue<string>() ?? string.Empty,
            node["practical_application"]?.GetValue<string>() ?? string.Empty
        );
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
