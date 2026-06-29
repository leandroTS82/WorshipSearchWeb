using System.Text.Json.Serialization;

namespace WorshipSearchWeb.Models;

public class SearchRestrictions
{
    [JsonPropertyName("excluded")]
    public List<string> Excluded { get; set; } = new();
}
