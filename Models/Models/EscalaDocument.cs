using System.Text.Json.Serialization;

namespace WorshipSearchWeb.Models;

public class EscalaDocument
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("titulo")]
    public string Titulo { get; set; } = string.Empty;

    [JsonPropertyName("observacao")]
    public string Observacao { get; set; } = string.Empty;

    [JsonPropertyName("ministro")]
    public string Ministro { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public string Data { get; set; } = string.Empty;

    [JsonPropertyName("musicas")]
    public List<EscalaMusica> Musicas { get; set; } = new();

    [JsonPropertyName("criado_em")]
    public string CriadoEm { get; set; } = string.Empty;

    [JsonPropertyName("atualizado_em")]
    public string AtualizadoEm { get; set; } = string.Empty;
}

public class EscalaMusica
{
    [JsonPropertyName("file_name")]
    public string FileName { get; set; } = string.Empty;

    [JsonPropertyName("titulo")]
    public string Titulo { get; set; } = string.Empty;

    [JsonPropertyName("artista")]
    public string Artista { get; set; } = string.Empty;

    [JsonPropertyName("youtube_url")]
    public string YoutubeUrl { get; set; } = string.Empty;

    [JsonPropertyName("momento")]
    public string Momento { get; set; } = string.Empty;

    [JsonPropertyName("obs")]
    public string Obs { get; set; } = string.Empty;
}
