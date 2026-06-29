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

            double score;
            int matchedTerms;
            if (string.IsNullOrWhiteSpace(query.Query))
            {
                score = 1.0;
                matchedTerms = 0;
            }
            else
            {
                (score, matchedTerms) = ScoreDocument(doc, terms);
            }

            if (matchedTerms > 0 || string.IsNullOrWhiteSpace(query.Query))
            {
                results.Add(new LibrarySearchResult
                {
                    Document = doc,
                    Score = score,
                    FileName = fileName,
                    MatchedTerms = matchedTerms
                });
            }
        }

        return results
            .OrderByDescending(r => r.MatchedTerms)
            .ThenByDescending(r => r.Score)
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

    private static (double score, int matchedTerms) ScoreDocument(MusicDocument doc, string[] terms)
    {
        double total = 0;
        int matchedTerms = 0;
        var search = doc.Search ?? new SearchMetadata();
        foreach (var term in terms)
        {
            double termScore = 0;
            termScore += ScoreField(doc.Title, term, weight: 10);
            termScore += ScoreField(doc.Artist, term, weight: 8);
            termScore += ScoreList(search.Themes, term, weight: 6);
            termScore += ScoreList(search.Keywords, term, weight: 5);
            termScore += ScoreList(search.BiblicalReferences, term, weight: 5);
            termScore += ScoreList(search.BiblicalTopics, term, weight: 4);
            termScore += ScoreList(search.BiblicalBooks, term, weight: 4);
            termScore += ScoreList(search.Contexts, term, weight: 4);
            termScore += ScoreList(search.Moods, term, weight: 3);
            termScore += ScoreList(search.WorshipMoments, term, weight: 6);
            termScore += ScoreField(doc.Summary, term, weight: 3);
            termScore += ScoreField(search.Occasion, term, weight: 3);
            termScore += ScoreList(search.Synonyms, term, weight: 3);
            termScore += ScoreList(search.BiblicalCharacters, term, weight: 3);
            termScore += ScoreField(doc.Lyrics, term, weight: 2);
            termScore += ScoreField(doc.Explanation, term, weight: 2);
            termScore += ScoreField(doc.PracticalApplication, term, weight: 1);

            if (termScore > 0) matchedTerms++;
            total += termScore;
        }
        return (total, matchedTerms);
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
