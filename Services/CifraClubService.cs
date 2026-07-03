using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace WorshipSearchWeb.Services;

public interface ICifraClubService
{
    Task<CifraClubResult?> FindAsync(string title, string artist);
}

public record CifraClubResult(
    string Url,
    string Dns,
    string Slug,
    string MatchedTitle,
    string MatchedArtist,
    string IdSong,
    string IdArtist,
    string Score);

public class CifraClubService : ICifraClubService
{
    private readonly HttpClient _http;
    private readonly ILogger<CifraClubService> _logger;

    private const string BaseUrl = "https://solr.sscdn.co/cc/c7/";

    public CifraClubService(HttpClient http, ILogger<CifraClubService> logger)
    {
        _http = http;
        _logger = logger;
    }

    // Strips " - LETRAS.MUS.BR", leading "NNN - " prefixes, and HTML entities
    public static string CleanArtist(string artist)
    {
        artist = WebUtility.HtmlDecode(artist);
        artist = Regex.Replace(artist, @"\s*-\s*LETRAS\.MUS\.BR\s*$", "", RegexOptions.IgnoreCase).Trim();
        artist = Regex.Replace(artist, @"^\d+\s*-\s*", "").Trim();
        return artist;
    }

    public static string CleanTitle(string title)
    {
        title = WebUtility.HtmlDecode(title).Trim();
        // For medleys (A / B / C), take only the first song
        if (title.Contains('/'))
            title = title.Split('/')[0].Trim();
        // Strip "(part. Artist)" and "(feat. Artist)"
        title = Regex.Replace(title, @"\s*\(\s*(?:part|feat)\..*?\)", "", RegexOptions.IgnoreCase).Trim();
        return title;
    }

    // Returns true if the song is a medley/pot-pourri that won't exist in CifraClub
    public static bool IsMedley(string title) =>
        title.Contains('/') || title.Contains("pot-pourri", StringComparison.OrdinalIgnoreCase);

    public async Task<CifraClubResult?> FindAsync(string title, string artist)
    {
        try
        {
            var cleanTitle  = CleanTitle(title);
            var cleanArtist = CleanArtist(artist);
            var q = Uri.EscapeDataString($"{cleanTitle} {cleanArtist}");
            var url = $"{BaseUrl}?q={q}&limit=30&callback=suggest_callback";

            string raw;
            try
            {
                raw = await _http.GetStringAsync(url);
            }
            catch (Exception httpEx)
            {
                _logger.LogWarning(httpEx, "CifraClub HTTP request failed for '{Title}' – '{Artist}'", title, artist);
                return null;
            }

            var json = StripJsonp(raw);
            if (string.IsNullOrWhiteSpace(json))
            {
                _logger.LogWarning("CifraClub empty JSONP for '{Title}'", title);
                return null;
            }

            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("response", out var response) ||
                !response.TryGetProperty("docs", out var docs))
            {
                _logger.LogWarning("CifraClub unexpected JSON shape for '{Title}': {Json}", title, json[..Math.Min(300, json.Length)]);
                return null;
            }

            var normTitle = Normalize(cleanTitle);
            var items = docs.EnumerateArray().ToList();

            _logger.LogDebug("CifraClub: {Count} results for '{Title}'", items.Count, title);

            // First pass: title+artist similarity
            foreach (var item in items)
            {
                var match = TryMatch(item, normTitle);
                if (match != null) return match;
            }

            // No match found
            if (items.Count > 0)
            {
                var first = items[0];
                var firstTitle = GetStr(first, "txt");
                _logger.LogDebug("CifraClub no match for '{Title}'. First result title='{R}'", title, firstTitle);
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CifraClub lookup failed for '{Title}' – '{Artist}'", title, artist);
            return null;
        }
    }

    private static CifraClubResult? TryMatch(JsonElement item, string normTitle)
    {
        var itemTitle  = GetStr(item, "txt");
        var itemArtist = GetStr(item, "art");
        var dns        = GetStr(item, "dns");
        var slug       = GetStr(item, "url");

        if (string.IsNullOrWhiteSpace(dns) || string.IsNullOrWhiteSpace(slug)) return null;

        if (!IsSimilar(normTitle, Normalize(itemTitle))) return null;

        return new CifraClubResult(
            Url:           $"https://www.cifraclub.com.br/{dns}/{slug}/",
            Dns:           dns,
            Slug:          slug,
            MatchedTitle:  itemTitle,
            MatchedArtist: itemArtist,
            IdSong:        GetStr(item, "id_song"),
            IdArtist:      GetStr(item, "id_artist"),
            Score:         GetStr(item, "score"));
    }

    private static string GetStr(JsonElement el, string prop)
    {
        if (el.TryGetProperty(prop, out var v))
            return v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : v.ToString();
        return "";
    }

    private static string StripJsonp(string raw)
    {
        var start = raw.IndexOf('(');
        var end   = raw.LastIndexOf(')');
        return (start >= 0 && end > start) ? raw[(start + 1)..end] : raw;
    }

    // Title similarity: bidirectional contains OR significant word overlap (≥60% of shorter)
    private static bool IsSimilar(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
        if (a.Contains(b) || b.Contains(a)) return true;

        var wa = Words(a);
        var wb = Words(b);
        if (wa.Count == 0 || wb.Count == 0) return false;
        var common = wa.Intersect(wb).Count();
        var threshold = (int)Math.Ceiling(Math.Min(wa.Count, wb.Count) * 0.6);
        return common >= Math.Max(1, threshold);
    }

    private static HashSet<string> Words(string s) =>
        s.Split(' ', StringSplitOptions.RemoveEmptyEntries)
         .Where(w => w.Length > 2)
         .ToHashSet();

    public static string Normalize(string s)
    {
        var formed = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(formed.Length);
        foreach (var c in formed)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        return sb.ToString().ToLowerInvariant();
    }
}
