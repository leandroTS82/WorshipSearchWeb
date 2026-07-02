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

    // chapter:verse pattern always means biblical_reference regardless of LLM output
    private static readonly Regex ChapterVersePattern = new(@"\d+:\d+", RegexOptions.Compiled);

    // Known Portuguese biblical book names (normalized, no diacritics)
    private static readonly HashSet<string> BiblicalBooks = new(StringComparer.OrdinalIgnoreCase)
    {
        "genesis","exodo","levitico","numeros","deuteronomio","josue","juizes","rute",
        "samuel","reis","cronicas","esdras","neemias","ester","jo","salmo","salmos",
        "proverbios","eclesiastes","canticos","isaias","jeremias","lamentacoes",
        "ezequiel","daniel","oseias","joel","amos","obadias","jonas","miqueias",
        "naum","habacuque","sofonias","ageu","zacarias","malaquias",
        "mateus","marcos","lucas","joao","atos","romanos","corintios","galatas",
        "efesios","filipenses","colossenses","tessalonicenses","timoteo","tito",
        "filemon","hebreus","tiago","pedro","judas","apocalipse"
    };

    private static string ApplyLocalOverride(string input, string llmType)
    {
        // "Isaías 41:10", "João 3:16" → always biblical_reference
        if (ChapterVersePattern.IsMatch(input)) return "biblical_reference";

        // "Salmo 91", "Isaías 41" → book name followed by chapter number
        var parts = NormalizeSimple(input.Trim()).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2 && int.TryParse(parts[^1], out _))
        {
            // Last token is a number; check if leading part is a known book
            var bookCandidate = parts.Length == 3 && int.TryParse(parts[0], out _)
                ? parts[1]   // "1 Samuel 3" → "samuel"
                : parts[^2]; // "Salmo 91" → "salmo", "Isaías 41" → "isaias"
            if (BiblicalBooks.Contains(bookCandidate))
                return "biblical_reference";
        }

        return llmType;
    }

    private static string NormalizeSimple(string s)
    {
        var formed = s.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder(formed.Length);
        foreach (var c in formed)
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) !=
                System.Globalization.UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        return sb.ToString().ToLowerInvariant();
    }

    public async Task<SearchCriterion> ClassifyAsync(string input)
    {
        var criterion = new SearchCriterion { Input = input, Type = "general", Tags = new SearchMetadata() };
        try
        {
            var prompt = PromptTemplates.CriterionClassificationTemplate.Replace("{input}", input);
            var (response, _) = await _groq.CompleteAsync(prompt);

            var cleaned = Regex.Replace(response, @"```(?:json)?", string.Empty).Trim();
            var start = cleaned.IndexOf('{');
            var end   = cleaned.LastIndexOf('}');
            if (start >= 0 && end > start)
            {
                var node = JsonNode.Parse(cleaned[start..(end + 1)]);
                if (node != null)
                {
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
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to classify criterion: {Input}", input);
        }
        // Local pattern always wins over LLM (more reliable for structured patterns)
        var overriddenType = ApplyLocalOverride(input, criterion.Type);
        if (overriddenType == "biblical_reference" && criterion.Type != "biblical_reference")
        {
            // LLM classified this differently and likely skipped biblical tag fields — seed a
            // minimal fallback so typed matching still has something to score against.
            if (criterion.Tags.BiblicalReferences.Count == 0)
                criterion.Tags.BiblicalReferences.Add(input.Trim());
        }
        criterion.Type = overriddenType;
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
