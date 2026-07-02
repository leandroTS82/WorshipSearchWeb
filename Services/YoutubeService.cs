namespace WorshipSearchWeb.Services;

public interface IYoutubeService
{
    Task<string?> ExtractIdFromLetrasUrlAsync(string letrasUrl);
}

public class YoutubeService : IYoutubeService
{
    private readonly HttpClient _http;
    private readonly ILogger<YoutubeService> _logger;

    public YoutubeService(HttpClient http, ILogger<YoutubeService> logger)
    {
        _http   = http;
        _logger = logger;
    }

    public async Task<string?> ExtractIdFromLetrasUrlAsync(string letrasUrl)
    {
        if (string.IsNullOrWhiteSpace(letrasUrl)) return null;
        try
        {
            var html = await _http.GetStringAsync(letrasUrl);
            var id = MusicHtmlParser.ExtractYoutubeId(html);
            return string.IsNullOrWhiteSpace(id) ? null : id;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "YouTube extraction failed for {Url}", letrasUrl);
            return null;
        }
    }
}
