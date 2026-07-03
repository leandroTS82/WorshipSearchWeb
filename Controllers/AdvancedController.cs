using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using WorshipSearchWeb.Services;

namespace WorshipSearchWeb.Controllers;

public class AdvancedController : Controller
{
    private readonly IMusicRepository _repository;
    private readonly ICifraClubService _cifraClub;
    private readonly IYoutubeService _youtube;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<AdvancedController> _logger;

    public AdvancedController(
        IMusicRepository repository,
        ICifraClubService cifraClub,
        IYoutubeService youtube,
        IWebHostEnvironment env,
        ILogger<AdvancedController> logger)
    {
        _repository = repository;
        _cifraClub  = cifraClub;
        _youtube    = youtube;
        _env        = env;
        _logger     = logger;
    }

    public IActionResult Index() => View();

    // ── Song list ──────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> GetSongs()
    {
        var all = await _repository.LoadAllAsync();
        var result = all.Select(x => new
        {
            fileName   = x.FileName,
            title      = x.Doc.Title,
            artist     = x.Doc.Artist,
            hasCifra   = !string.IsNullOrWhiteSpace(x.Doc.Metadata?.CifraClubUrl),
            cifraUrl   = x.Doc.Metadata?.CifraClubUrl ?? "",
            hasYoutube = !string.IsNullOrWhiteSpace(x.Doc.Metadata?.YoutubeId),
            youtubeId  = x.Doc.Metadata?.YoutubeId ?? "",
            letrasUrl  = x.Doc.Metadata?.CanonicalUrl ?? ""
        }).ToList();
        return Json(result);
    }

    // ── CifraClub ──────────────────────────────────────────────────────────

    [HttpPost]
    public async Task<IActionResult> EnrichCifra([FromBody] EnrichCifraRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.FileName))
            return BadRequest(new { error = "fileName required" });

        var doc = await _repository.LoadAsync(req.FileName);
        if (doc == null) return NotFound(new { error = "Not found" });

        if (CifraClubService.IsMedley(doc.Title))
            return Json(new { success = true, found = false, skipped = true, url = "", matchedTitle = "", fileName = req.FileName, title = doc.Title, artist = doc.Artist });

        var found = await _cifraClub.FindAsync(doc.Title, doc.Artist);
        if (found != null)
        {
            doc.Metadata.CifraClubUrl        = found.Url;
            doc.Metadata.CifraclubDns        = found.Dns;
            doc.Metadata.CifraclubSlug       = found.Slug;
            doc.Metadata.CifraclubMatchedTitle  = found.MatchedTitle;
            doc.Metadata.CifraclubMatchedArtist = found.MatchedArtist;
            doc.Metadata.CifraclubIdSong     = found.IdSong;
            doc.Metadata.CifraclubIdArtist   = found.IdArtist;
            doc.Metadata.CifraclubScore      = found.Score;
            await _repository.SaveAsync(doc);
            _logger.LogInformation("CifraClub enriched {Title}: {Url}", doc.Title, found.Url);
        }

        return Json(new
        {
            success      = true,
            found        = found != null,
            url          = found?.Url ?? "",
            matchedTitle = found?.MatchedTitle ?? "",
            fileName     = req.FileName,
            title        = doc.Title,
            artist       = doc.Artist
        });
    }

    // ── YouTube ────────────────────────────────────────────────────────────

    [HttpPost]
    public async Task<IActionResult> EnrichYoutube([FromBody] EnrichCifraRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.FileName))
            return BadRequest(new { error = "fileName required" });

        var doc = await _repository.LoadAsync(req.FileName);
        if (doc == null) return NotFound(new { error = "Not found" });

        var letrasUrl = doc.Metadata?.CanonicalUrl;
        if (string.IsNullOrWhiteSpace(letrasUrl))
            return Json(new { success = true, found = false, youtubeId = "", fileName = req.FileName, title = doc.Title, artist = doc.Artist });

        var id = await _youtube.ExtractIdFromLetrasUrlAsync(letrasUrl);
        if (!string.IsNullOrWhiteSpace(id))
        {
            doc.Metadata!.YoutubeId = id;
            await _repository.SaveAsync(doc);
            _logger.LogInformation("YouTube enriched {Title}: {Id}", doc.Title, id);
        }

        return Json(new
        {
            success   = true,
            found     = !string.IsNullOrWhiteSpace(id),
            youtubeId = id ?? "",
            fileName  = req.FileName,
            title     = doc.Title,
            artist    = doc.Artist
        });
    }

    // ── Reports ────────────────────────────────────────────────────────────

    [HttpPost]
    public async Task<IActionResult> SaveReport([FromBody] SaveReportRequest req)
    {
        if (req.Type != "cifra" && req.Type != "youtube")
            return BadRequest(new { error = "type must be 'cifra' or 'youtube'" });

        var fileName = req.Type == "cifra"
            ? "cifraclub-not-found.json"
            : "youtube-not-found.json";

        var jsonDir = Path.Combine(_env.ContentRootPath, "json");
        Directory.CreateDirectory(jsonDir);

        var filePath = Path.Combine(jsonDir, fileName);

        var report = req.Type == "cifra"
            ? BuildCifraReport(req.Songs)
            : BuildYoutubeReport(req.Songs);

        var options = new JsonSerializerOptions { WriteIndented = true };
        var content = JsonSerializer.Serialize(report, options);
        await System.IO.File.WriteAllTextAsync(filePath, content);

        return Json(new { success = true, file = fileName, count = req.Songs?.Count ?? 0 });
    }

    [HttpGet]
    public IActionResult DownloadReport([FromQuery] string type)
    {
        var fileName = type == "cifra"
            ? "cifraclub-not-found.json"
            : "youtube-not-found.json";

        var jsonDir = Path.Combine(_env.ContentRootPath, "json");
        Directory.CreateDirectory(jsonDir);

        var filePath = Path.Combine(jsonDir, fileName);
        if (!System.IO.File.Exists(filePath))
            return NotFound(new { error = "Report not generated yet. Run enrichment first." });

        var bytes = System.IO.File.ReadAllBytes(filePath);
        return File(bytes, "application/json", fileName);
    }

    // ── Report builders ────────────────────────────────────────────────────

    private static object BuildCifraReport(List<NotFoundSong>? songs)
    {
        return new
        {
            _instructions = new
            {
                description = "Arquivo gerado automaticamente pelo WorshipSearch. Contém músicas sem correspondência no CifraClub após enriquecimento em lote.",
                how_it_works = "O sistema busca em https://solr.sscdn.co/cc/c7/ usando título + artista (sem o sufixo LETRAS.MUS.BR). Quando não há correspondência, a música entra nesta lista.",
                how_to_fix = new[]
                {
                    "1. Copie o campo 'solr_search_url' de uma música e abra no Postman (GET). Essa URL já usa o artista limpo.",
                    "2. Analise os resultados retornados (campos: txt=título, art=artista, dns=slug artista, url=slug música).",
                    "3. Se a URL retornar algo próximo, ajuste a lógica de matching em CifraClubService.cs → IsSimilar().",
                    "4. Se não retornar nada, experimente o campo 'solr_search_url_title_only' para buscar só pelo título.",
                    "5. O link do CifraClub é: https://www.cifraclub.com.br/{dns}/{url}/"
                },
                postman_setup = new
                {
                    method = "GET",
                    headers = new { },
                    note = "Nenhum header especial necessário. A API é pública."
                }
            },
            generated_at    = DateTime.UtcNow.ToString("O"),
            total_not_found = songs?.Count ?? 0,
            songs = songs?.Select(s =>
            {
                var cleanTitle  = CifraClubService.CleanTitle(s.Title);
                var cleanArtist = CifraClubService.CleanArtist(s.Artist);
                return new
                {
                    title                       = s.Title,
                    artist                      = s.Artist,
                    clean_title                 = cleanTitle,
                    clean_artist                = cleanArtist,
                    letras_url                  = s.LetrasUrl,
                    solr_search_url             = $"https://solr.sscdn.co/cc/c7/?q={Uri.EscapeDataString($"{cleanTitle} {cleanArtist}")}&limit=30&callback=suggest_callback",
                    solr_search_url_title_only  = $"https://solr.sscdn.co/cc/c7/?q={Uri.EscapeDataString(cleanTitle)}&limit=30&callback=suggest_callback",
                    cifraclub_manual_search_url = $"https://www.cifraclub.com.br/?q={Uri.EscapeDataString($"{cleanTitle} {cleanArtist}")}",
                    json_file                   = s.FileName
                };
            }).ToList()
        };
    }

    private static object BuildYoutubeReport(List<NotFoundSong>? songs)
    {
        return new
        {
            _instructions = new
            {
                description = "Arquivo gerado automaticamente pelo WorshipSearch. Contém músicas sem ID do YouTube após enriquecimento em lote.",
                how_it_works = "O sistema acessa cada URL do letras.mus.br e procura o campo 'YoutubeID' no script da página.",
                how_to_fix = new[]
                {
                    "1. Copie o campo 'letras_url' e acesse no navegador.",
                    "2. Pressione F12 → Fontes / Sources → procure por 'YoutubeID' no HTML da página.",
                    "3. Se existir, o regex em MusicHtmlParser.cs → ExtractYoutubeId() deve capturar. Teste o regex: \"YoutubeID\"\\s*:\\s*\"([^\")]{5,20})\"",
                    "4. Se a música não tiver YouTube associado no letras.mus.br, busque manualmente no YouTube e adicione pelo editor da música.",
                    "5. Para testar via Postman: GET no campo 'postman_get_url' e procure 'YoutubeID' no corpo da resposta."
                },
                postman_setup = new
                {
                    method = "GET",
                    headers = new
                    {
                        UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120.0 Safari/537.36"
                    },
                    note = "Use o User-Agent acima. Sem ele o letras.mus.br pode retornar conteúdo diferente ou bloquear."
                }
            },
            generated_at    = DateTime.UtcNow.ToString("O"),
            total_not_found = songs?.Count ?? 0,
            songs = songs?.Select(s => new
            {
                title           = s.Title,
                artist          = s.Artist,
                letras_url      = s.LetrasUrl,
                postman_get_url = s.LetrasUrl,
                json_file       = s.FileName
            }).ToList()
        };
    }
}

public class EnrichCifraRequest
{
    public string FileName { get; set; } = string.Empty;
}

public class NotFoundSong
{
    public string Title     { get; set; } = string.Empty;
    public string Artist    { get; set; } = string.Empty;
    public string LetrasUrl { get; set; } = string.Empty;
    public string FileName  { get; set; } = string.Empty;
}

public class SaveReportRequest
{
    public string Type { get; set; } = string.Empty;
    public List<NotFoundSong>? Songs { get; set; }
}
