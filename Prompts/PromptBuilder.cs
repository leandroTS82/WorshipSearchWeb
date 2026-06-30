using System.Text.Json;
using WorshipSearchWeb.Models;

namespace WorshipSearchWeb.Prompts;

public interface IPromptBuilder
{
    string BuildParagraphPrompt(string paragraph, string title, string artist, SearchMetadata existing);
    string BuildSummaryPrompt(MusicDocument doc);
}

public class PromptBuilder : IPromptBuilder
{
    private static string Join(List<string> list) =>
        list.Count == 0 ? "(none)" : string.Join(", ", list.Select(x => $"\"{x}\""));

    public string BuildParagraphPrompt(string paragraph, string title, string artist, SearchMetadata existing)
    {
        return PromptTemplates.ParagraphEnrichmentTemplate
            .Replace("{title}", title)
            .Replace("{artist}", artist)
            .Replace("{paragraph}", paragraph)
            .Replace("{existing_themes}", Join(existing.Themes))
            .Replace("{existing_moods}", Join(existing.Moods))
            .Replace("{existing_contexts}", Join(existing.Contexts))
            .Replace("{existing_keywords}", Join(existing.Keywords))
            .Replace("{existing_biblical_topics}", Join(existing.BiblicalTopics))
            .Replace("{existing_biblical_references}", Join(existing.BiblicalReferences))
            .Replace("{existing_biblical_books}", Join(existing.BiblicalBooks))
            .Replace("{existing_biblical_characters}", Join(existing.BiblicalCharacters))
            .Replace("{existing_synonyms}", Join(existing.Synonyms))
            .Replace("{existing_worship_style}", existing.WorshipStyle)
            .Replace("{existing_energy_level}", existing.EnergyLevel)
            .Replace("{existing_occasion}", existing.Occasion)
            .Replace("{existing_worship_moments}", Join(existing.WorshipMoments));
    }

    public string BuildSummaryPrompt(MusicDocument doc)
    {
        var tagsJson = JsonSerializer.Serialize(doc.Search, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });

        // Use only unique strophes — repeated choruses inflate token count without adding context.
        var uniqueLyrics = GetUniqueParagraphs(doc.Lyrics ?? string.Empty);

        return PromptTemplates.SummaryTemplate
            .Replace("{title}", doc.Title)
            .Replace("{artist}", doc.Artist)
            .Replace("{tags_json}", tagsJson)
            .Replace("{lyrics}", uniqueLyrics);
    }

    private static string GetUniqueParagraphs(string lyrics)
    {
        var paragraphs = System.Text.RegularExpressions.Regex
            .Split(lyrics, @"\n[ \t]*\n|\r\n[ \t]*\r\n")
            .Select(p => p.Trim())
            .Where(p => p.Length > 10)
            .ToList();

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var unique = new List<string>();
        foreach (var p in paragraphs)
        {
            var key = System.Text.RegularExpressions.Regex.Replace(p, @"\s+", " ").ToLowerInvariant();
            if (seen.Add(key)) unique.Add(p);
        }
        return string.Join("\n\n", unique);
    }
}
