namespace WorshipSearchWeb.Models;

public class SearchResult
{
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string Genre { get; set; } = string.Empty;
    public string ArtistSlug { get; set; } = string.Empty;
    public string SongSlug { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
    public double Popularity { get; set; }
    public string Url => $"https://www.letras.mus.br/{ArtistSlug}/{SongSlug}/";
}

public class LibrarySearchResult
{
    public MusicDocument Document { get; set; } = new();
    public double Score { get; set; }
    public string FileName { get; set; } = string.Empty;
    public int MatchedTerms { get; set; }
}
