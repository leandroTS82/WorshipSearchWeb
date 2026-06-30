using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using WorshipSearchWeb.Models;
using WorshipSearchWeb.Prompts;

namespace WorshipSearchWeb.Services;

public interface ISearchIndexClassifier
{
    Task<SearchCriterion> ClassifyAsync(string input);
}

public class SearchIndexClassifierService : ISearchIndexClassifier
{
    private readonly IGroqService _groq;
    private readonly ILogger<SearchIndexClassifierService> _logger;

    public SearchIndexClassifierService(IGroqService groq, ILogger<SearchIndexClassifierService> logger)
    {
        _groq = groq;
        _logger = logger;
    }

    public async Task<SearchCriterion> ClassifyAsync(string input)
    {
        var criterion = new SearchCriterion { Input = input, Type = "general", Tags = new SearchMetadata() };
        try
        {
            var prompt = PromptTemplates.CriterionClassificationTemplate.Replace("{input}", input);
            var response = await _groq.CompleteAsync(prompt);

            var cleaned = Regex.Replace(response, @"```(?:json)?", string.Empty).Trim();
            var start = cleaned.IndexOf('{');
            var end   = cleaned.LastIndexOf('}');
            if (start < 0 || end <= start) return criterion;

            var node = JsonNode.Parse(cleaned[start..(end + 1)]);
            if (node == null) return criterion;

            criterion.Type = node["type"]?.GetValue<string>() ?? "general";

            var tagsNode = node["tags"];
            if (tagsNode != null)
            {
                criterion.Tags = new SearchMetadata
                {
                    Themes             = ParseArray(tagsNode["themes"]),
                    Contexts           = ParseArray(tagsNode["contexts"]),
                    Keywords           = ParseArray(tagsNode["keywords"]),
                    BiblicalTopics     = ParseArray(tagsNode["biblical_topics"]),
                    BiblicalReferences = ParseArray(tagsNode["biblical_references"]),
                    BiblicalBooks      = ParseArray(tagsNode["biblical_books"]),
                    BiblicalCharacters = ParseArray(tagsNode["biblical_characters"]),
                    Moods              = ParseArray(tagsNode["moods"]),
                    Synonyms           = ParseArray(tagsNode["synonyms"]),
                    WorshipMoments     = ParseArray(tagsNode["worship_moments"]),
                    WorshipStyle       = tagsNode["worship_style"]?.GetValue<string>() ?? string.Empty,
                    EnergyLevel        = tagsNode["energy_level"]?.GetValue<string>() ?? string.Empty,
                    Occasion           = tagsNode["occasion"]?.GetValue<string>() ?? string.Empty,
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to classify criterion: {Input}", input);
        }
        return criterion;
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
