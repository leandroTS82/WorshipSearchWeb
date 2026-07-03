using WorshipSearchWeb.Models;

namespace WorshipSearchWeb.Services;

public interface IMusicJsonGenerator
{
    MusicDocument Generate(ParsedMusic parsed);
}

public class MusicJsonGenerator : IMusicJsonGenerator
{
    public MusicDocument Generate(ParsedMusic parsed)
    {
        var id = BuildId(parsed.Artist, parsed.Title);

        return new MusicDocument
        {
            Id = id,
            Title = parsed.Title,
            Artist = parsed.Artist,
            Album = string.Empty,
            Language = "pt-BR",
            Genre = parsed.Genre,
            Status = "review",
            Lyrics = parsed.Lyrics,
            Metadata = new MusicMetadata
            {
                Source = "letras.mus.br",
                CanonicalUrl = parsed.CanonicalUrl,
                Image = parsed.Image,
                IndexedAt = DateTime.UtcNow.ToString("O"),
                YoutubeId = parsed.YoutubeId
            },
            Search = new SearchMetadata()
        };
    }

    public static string BuildId(string artist, string title)
    {
        var artistSlug = Slugify(artist);
        var titleSlug = Slugify(title);
        return $"{artistSlug}_{titleSlug}";
    }

    public static string Slugify(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "unknown";

        // Normalize unicode to ASCII equivalents
        var normalized = text.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder();

        foreach (var c in normalized)
        {
            var category = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != System.Globalization.UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        var ascii = sb.ToString().ToLowerInvariant();

        // Replace non-alphanumeric with dash
        var slug = System.Text.RegularExpressions.Regex.Replace(ascii, @"[^a-z0-9]+", "-");
        slug = slug.Trim('-');

        return slug;
    }
}
