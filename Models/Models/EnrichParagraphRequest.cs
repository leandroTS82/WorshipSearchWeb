using System.Text.Json.Serialization;

namespace WorshipSearchWeb.Models;

public class EnrichParagraphRequest
{
    [JsonPropertyName("paragraph")]
    public string Paragraph { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("artist")]
    public string Artist { get; set; } = string.Empty;

    [JsonPropertyName("existing_tags")]
    public SearchMetadata ExistingTags { get; set; } = new();
}
