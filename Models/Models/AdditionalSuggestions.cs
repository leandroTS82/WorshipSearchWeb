using System.Text.Json.Serialization;

namespace WorshipSearchWeb.Models;

public class AdditionalSuggestions
{
    [JsonPropertyName("items")]
    public List<string> Items { get; set; } = new();
}
