using System.Text.Json.Serialization;

namespace WorshipSearchWeb.Models;

public class MusicDocument
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("artist")]
    public string Artist { get; set; } = string.Empty;

    [JsonPropertyName("album")]
    public string Album { get; set; } = string.Empty;

    [JsonPropertyName("language")]
    public string Language { get; set; } = "pt-BR";

    [JsonPropertyName("genre")]
    public string Genre { get; set; } = string.Empty;

    [JsonPropertyName("approved")]
    public bool Approved { get; set; } = false;

    [JsonPropertyName("search")]
    public SearchMetadata Search { get; set; } = new();

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("explanation")]
    public string Explanation { get; set; } = string.Empty;

    [JsonPropertyName("practical_application")]
    public string PracticalApplication { get; set; } = string.Empty;

    [JsonPropertyName("lyrics")]
    public string Lyrics { get; set; } = string.Empty;

    [JsonPropertyName("metadata")]
    public MusicMetadata Metadata { get; set; } = new();
}

public class SearchMetadata
{
    [JsonPropertyName("themes")]
    public List<string> Themes { get; set; } = new();

    [JsonPropertyName("contexts")]
    public List<string> Contexts { get; set; } = new();

    [JsonPropertyName("keywords")]
    public List<string> Keywords { get; set; } = new();

    [JsonPropertyName("biblical_topics")]
    public List<string> BiblicalTopics { get; set; } = new();

    [JsonPropertyName("biblical_references")]
    public List<string> BiblicalReferences { get; set; } = new();

    [JsonPropertyName("biblical_books")]
    public List<string> BiblicalBooks { get; set; } = new();

    [JsonPropertyName("biblical_characters")]
    public List<string> BiblicalCharacters { get; set; } = new();

    [JsonPropertyName("moods")]
    public List<string> Moods { get; set; } = new();

    [JsonPropertyName("synonyms")]
    public List<string> Synonyms { get; set; } = new();

    [JsonPropertyName("worship_style")]
    public string WorshipStyle { get; set; } = string.Empty;

    [JsonPropertyName("energy_level")]
    public string EnergyLevel { get; set; } = string.Empty;

    [JsonPropertyName("occasion")]
    public string Occasion { get; set; } = string.Empty;

    [JsonPropertyName("worship_moments")]
    public List<string> WorshipMoments { get; set; } = new();
}

public class MusicMetadata
{
    [JsonPropertyName("source")]
    public string Source { get; set; } = "letras.mus.br";

    [JsonPropertyName("canonical_url")]
    public string CanonicalUrl { get; set; } = string.Empty;

    [JsonPropertyName("image")]
    public string Image { get; set; } = string.Empty;

    [JsonPropertyName("indexed_at")]
    public string IndexedAt { get; set; } = string.Empty;

    [JsonPropertyName("youtube_url")]
    public string YoutubeUrl { get; set; } = string.Empty;
}
