using System.Text.Json.Serialization;

namespace WorshipSearchWeb.Models;

public class SearchHistoryEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

    [JsonPropertyName("query")]
    public string Query { get; set; } = string.Empty;

    [JsonPropertyName("searched_at")]
    public string SearchedAt { get; set; } = DateTime.UtcNow.ToString("O");

    [JsonPropertyName("result_count")]
    public int ResultCount { get; set; }

    [JsonPropertyName("selected_title")]
    public string? SelectedTitle { get; set; }

    [JsonPropertyName("selected_artist")]
    public string? SelectedArtist { get; set; }

    [JsonPropertyName("picked_manually")]
    public bool PickedManually { get; set; }
}
