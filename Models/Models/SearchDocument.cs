using System.Text.Json.Serialization;

namespace WorshipSearchWeb.Models;

public class SearchDocument
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("searched_at")]
    public string SearchedAt { get; set; } = string.Empty;

    [JsonPropertyName("raw_query")]
    public string RawQuery { get; set; } = string.Empty;

    [JsonPropertyName("criteria")]
    public List<SearchCriterion> Criteria { get; set; } = new();

    [JsonPropertyName("result_count")]
    public int ResultCount { get; set; }

    [JsonPropertyName("selected_songs")]
    public List<string> SelectedSongs { get; set; } = new();
}

public class SearchCriterion
{
    [JsonPropertyName("input")]
    public string Input { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "general";

    [JsonPropertyName("tags")]
    public SearchMetadata Tags { get; set; } = new();
}
