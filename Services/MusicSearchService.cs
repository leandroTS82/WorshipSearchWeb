using System.Text.Json;
using System.Text.RegularExpressions;
using WorshipSearchWeb.Models;

namespace WorshipSearchWeb.Services;

public interface IMusicSearchService
{
    Task<List<SearchResult>> SearchAsync(string query);
}

public class MusicSearchService : IMusicSearchService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<MusicSearchService> _logger;

    private const string SolrUrl = "https://solr.sscdn.co/letras/m1/?q={0}&wt=json&callback=LetrasSug";

    public MusicSearchService(HttpClient httpClient, ILogger<MusicSearchService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<List<SearchResult>> SearchAsync(string query)
    {
        var url = string.Format(SolrUrl, Uri.EscapeDataString(query));

        var response = await _httpClient.GetStringAsync(url);

        // Strip JSONP wrapper: LetrasSug({...})
        var jsonpMatch = Regex.Match(response, @"LetrasSug\((.+)\)\s*$", RegexOptions.Singleline);
        if (!jsonpMatch.Success)
        {
            _logger.LogWarning("Unexpected response format from Solr");
            return new List<SearchResult>();
        }

        var json = jsonpMatch.Groups[1].Value;
        var doc = JsonDocument.Parse(json);

        var results = new List<SearchResult>();

        if (!doc.RootElement.TryGetProperty("response", out var responseEl) ||
            !responseEl.TryGetProperty("docs", out var docs))
        {
            return results;
        }

        foreach (var item in docs.EnumerateArray())
        {
            // t == "2" means song
            if (!item.TryGetProperty("t", out var tProp) || tProp.GetString() != "2")
                continue;

            var result = new SearchResult
            {
                Title = GetString(item, "txt"),
                Artist = GetString(item, "art"),
                Genre = GetString(item, "g"),
                ArtistSlug = GetString(item, "dns"),
                SongSlug = GetString(item, "url"),
                Image = GetString(item, "imgm"),
            };

            if (item.TryGetProperty("h", out var hProp))
            {
                if (hProp.ValueKind == JsonValueKind.Number)
                    result.Popularity = hProp.GetDouble();
                else if (double.TryParse(hProp.GetString(), out var h))
                    result.Popularity = h;
            }

            if (!string.IsNullOrWhiteSpace(result.Title))
                results.Add(result);
        }

        return results;
    }

    private static string GetString(JsonElement element, string property)
    {
        if (element.TryGetProperty(property, out var prop))
            return prop.GetString() ?? string.Empty;
        return string.Empty;
    }
}
