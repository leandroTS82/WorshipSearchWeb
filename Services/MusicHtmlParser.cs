using HtmlAgilityPack;

namespace WorshipSearchWeb.Services;

public class ParsedMusic
{
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
    public string CanonicalUrl { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Lyrics { get; set; } = string.Empty;
    public string Genre { get; set; } = string.Empty;
}

public interface IMusicHtmlParser
{
    Task<ParsedMusic> ParseAsync(string url);
}

public class MusicHtmlParser : IMusicHtmlParser
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<MusicHtmlParser> _logger;

    public MusicHtmlParser(HttpClient httpClient, ILogger<MusicHtmlParser> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ParsedMusic> ParseAsync(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120.0 Safari/537.36");

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync();

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var result = new ParsedMusic();

        // Extract meta tags
        result.Title = GetMetaContent(doc, "og:title") ?? GetMetaContent(doc, "title") ?? string.Empty;
        result.Image = GetMetaContent(doc, "og:image") ?? string.Empty;
        result.CanonicalUrl = GetMetaContent(doc, "og:url") ?? url;
        result.Description = GetMetaContent(doc, "description") ?? GetMetaContent(doc, "og:description") ?? string.Empty;

        // Clean up title: "Song - Artist | Letras.mus.br" -> extract song/artist
        if (result.Title.Contains(" - "))
        {
            var parts = result.Title.Split(" - ", 2);
            result.Title = parts[0].Trim();
            // artist part may have " | Letras" suffix
            var artistPart = parts[1];
            if (artistPart.Contains(" | "))
                artistPart = artistPart.Split(" | ")[0];
            result.Artist = artistPart.Trim();
        }

        // Find lyrics div — ordered from most to least specific
        var lyricsNode = doc.DocumentNode.SelectSingleNode("//div[contains(@class,'cnt-letra')]")
            ?? doc.DocumentNode.SelectSingleNode("//div[contains(@class,'lyric-original')]")
            ?? doc.DocumentNode.SelectSingleNode("//div[@id='letra-cnt']")
            ?? doc.DocumentNode.SelectSingleNode("//article[contains(@class,'lyric')]");

        if (lyricsNode != null)
        {
            result.Lyrics = ParseLyrics(lyricsNode);
        }
        else
        {
            _logger.LogWarning("Could not find lyrics node in {Url}", url);
        }

        return result;
    }

    private static string ParseLyrics(HtmlNode node)
    {
        var sb = new System.Text.StringBuilder();

        // Only process <p> tags — each <p> is a stanza; <br> inside = line break
        var paragraphs = node.SelectNodes(".//p");
        if (paragraphs != null)
        {
            foreach (var p in paragraphs)
            {
                foreach (var child in p.ChildNodes)
                {
                    if (child.Name == "br")
                        sb.AppendLine();
                    else if (child.Name == "i" || child.Name == "b" || child.Name == "span")
                        sb.Append(HtmlEntity.DeEntitize(child.InnerText));
                    else if (child.NodeType == HtmlNodeType.Text)
                        sb.Append(HtmlEntity.DeEntitize(child.InnerText));
                }
                sb.AppendLine();
                sb.AppendLine();
            }
        }
        else
        {
            // Fallback: no <p> tags, just replace <br> with newlines
            var html = node.InnerHtml;
            html = System.Text.RegularExpressions.Regex.Replace(html, @"<br\s*/?>", "\n", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            html = System.Text.RegularExpressions.Regex.Replace(html, @"<[^>]+>", string.Empty);
            sb.Append(HtmlEntity.DeEntitize(html));
        }

        return sb.ToString().Trim();
    }

    private static string? GetMetaContent(HtmlDocument doc, string nameOrProperty)
    {
        // Try og: property
        var node = doc.DocumentNode.SelectSingleNode($"//meta[@property='{nameOrProperty}']")
            ?? doc.DocumentNode.SelectSingleNode($"//meta[@name='{nameOrProperty}']");
        return node?.GetAttributeValue("content", null);
    }
}
