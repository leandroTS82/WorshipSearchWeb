using WorshipSearchWeb.Models;

namespace WorshipSearchWeb.Services;

public interface IJsonSearchEngine
{
    Task<List<LibrarySearchResult>> SearchAsync(SearchQuery query);
}

public class JsonSearchEngine : IJsonSearchEngine
{
    private readonly IMusicRepository _repository;
    private readonly ISearchRestrictionsService _restrictions;

    public JsonSearchEngine(IMusicRepository repository, ISearchRestrictionsService restrictions)
    {
        _repository = repository;
        _restrictions = restrictions;
    }

    public async Task<List<LibrarySearchResult>> SearchAsync(SearchQuery query)
    {
        var all = await _repository.LoadAllAsync();
        var restrictions = await _restrictions.LoadAsync();
        var excluded = restrictions.Excluded
            .Select(e => e.ToLowerInvariant())
            .ToHashSet();

        var results = new List<LibrarySearchResult>();

        var terms = string.IsNullOrWhiteSpace(query.Query)
            ? Array.Empty<string>()
            : query.Query.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        foreach (var (fileName, doc) in all)
        {
            if (query.ApprovedOnly && !doc.Approved) continue;
            if (excluded.Count > 0 && IsExcluded(doc, excluded)) continue;

            double score = string.IsNullOrWhiteSpace(query.Query)
                ? 1.0
                : ScoreDocument(doc, terms);

            if (score > 0 || string.IsNullOrWhiteSpace(query.Query))
            {
                results.Add(new LibrarySearchResult
                {
                    Document = doc,
                    Score = score,
                    FileName = fileName
                });
            }
        }

        return results
            .OrderByDescending(r => r.Score)
            .ThenByDescending(r => r.Document.Approved)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();
    }

    private static bool IsExcluded(MusicDocument doc, HashSet<string> excluded)
    {
        var genre = doc.Genre?.ToLowerInvariant() ?? string.Empty;
        var style = doc.Search?.WorshipStyle?.ToLowerInvariant() ?? string.Empty;

        foreach (var term in excluded)
        {
            if (genre.Contains(term) || style.Contains(term))
                return true;
        }
        return false;
    }

    private static double ScoreDocument(MusicDocument doc, string[] terms)
    {
        double total = 0;
        var search = doc.Search ?? new SearchMetadata();
        foreach (var term in terms)
        {
            total += ScoreField(doc.Title, term, weight: 10);
            total += ScoreField(doc.Artist, term, weight: 8);
            total += ScoreList(search.Themes, term, weight: 6);
            total += ScoreList(search.Keywords, term, weight: 5);
            total += ScoreList(search.BiblicalReferences, term, weight: 5);
            total += ScoreList(search.BiblicalTopics, term, weight: 4);
            total += ScoreList(search.BiblicalBooks, term, weight: 4);
            total += ScoreList(search.Contexts, term, weight: 4);
            total += ScoreList(search.Moods, term, weight: 3);
            total += ScoreList(search.WorshipMoments, term, weight: 6);
            total += ScoreField(doc.Summary, term, weight: 3);
            total += ScoreField(search.Occasion, term, weight: 3);
            total += ScoreList(search.Synonyms, term, weight: 3);
            total += ScoreList(search.BiblicalCharacters, term, weight: 3);
            total += ScoreField(doc.Lyrics, term, weight: 2);
            total += ScoreField(doc.Explanation, term, weight: 2);
            total += ScoreField(doc.PracticalApplication, term, weight: 1);
        }
        return total;
    }

    private static double ScoreField(string field, string term, double weight)
    {
        if (string.IsNullOrWhiteSpace(field)) return 0;
        var lower = field.ToLowerInvariant();
        if (lower == term) return weight * 2;
        if (lower.Contains(term)) return weight;
        return 0;
    }

    private static double ScoreList(List<string>? list, string term, double weight)
    {
        if (list == null) return 0;
        double score = 0;
        foreach (var item in list)
            score += ScoreField(item, term, weight);
        return score;
    }
}
