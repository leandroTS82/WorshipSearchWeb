using System.Globalization;
using System.Text;
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

    // Common Portuguese words that carry no search meaning
    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "de", "da", "do", "dos", "das", "e", "a", "o", "os", "as",
        "em", "no", "na", "nos", "nas", "por", "para", "com", "um", "uma",
        "ao", "que", "se", "sua", "seu", "seus", "suas", "ao", "eh", "sao"
    };

    public async Task<List<LibrarySearchResult>> SearchAsync(SearchQuery query)
    {
        var all = await _repository.LoadAllAsync();
        var restrictions = await _restrictions.LoadAsync();
        var excluded = restrictions.Excluded
            .Select(e => Normalize(e))
            .ToHashSet();

        var results = new List<LibrarySearchResult>();

        var terms = string.IsNullOrWhiteSpace(query.Query)
            ? Array.Empty<string>()
            : query.Query
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(Normalize)
                .Where(t => t.Length > 1 && !StopWords.Contains(t))
                .Distinct()
                .ToArray();

        foreach (var (fileName, doc) in all)
        {
            if (query.ApprovedOnly && !doc.Approved) continue;
            if (excluded.Count > 0 && IsExcluded(doc, excluded)) continue;

            double score;
            int matchedTerms;
            if (terms.Length == 0)
            {
                score = 1.0;
                matchedTerms = 0;
            }
            else
            {
                (score, matchedTerms) = ScoreDocument(doc, terms);
            }

            if (matchedTerms > 0 || terms.Length == 0)
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
        var genre = Normalize(doc.Genre ?? string.Empty);
        var style = Normalize(doc.Search?.WorshipStyle ?? string.Empty);

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

            // Core identity fields
            termScore += ScoreField(doc.Title, term, weight: 10);
            termScore += ScoreField(doc.Artist, term, weight: 8);

            // Tag fields — primary search surface
            termScore += ScoreList(search.Themes, term, weight: 7);
            termScore += ScoreList(search.Keywords, term, weight: 6);
            termScore += ScoreList(search.WorshipMoments, term, weight: 6);
            termScore += ScoreList(search.BiblicalReferences, term, weight: 5);
            termScore += ScoreList(search.BiblicalTopics, term, weight: 5);
            termScore += ScoreList(search.Contexts, term, weight: 5);
            termScore += ScoreList(search.Moods, term, weight: 4);
            termScore += ScoreList(search.BiblicalBooks, term, weight: 4);
            termScore += ScoreList(search.Synonyms, term, weight: 4);
            termScore += ScoreList(search.BiblicalCharacters, term, weight: 3);

            // Context string fields (WorshipStyle, EnergyLevel, Occasion)
            termScore += ScoreField(search.WorshipStyle, term, weight: 5);
            termScore += ScoreField(search.EnergyLevel, term, weight: 4);
            termScore += ScoreField(search.Occasion, term, weight: 4);

            // Descriptive text fields
            termScore += ScoreField(doc.Summary, term, weight: 3);
            termScore += ScoreField(doc.Explanation, term, weight: 2);
            termScore += ScoreField(doc.PracticalApplication, term, weight: 2);
            termScore += ScoreField(doc.Lyrics, term, weight: 1);

            if (termScore > 0) matchedTerms++;
            total += termScore;
        }

        return (total, matchedTerms);
    }

    private static double ScoreField(string field, string term, double weight)
    {
        if (string.IsNullOrWhiteSpace(field)) return 0;
        var lower = Normalize(field);
        if (lower == term) return weight * 2;
        if (lower.Contains(term)) return weight;
        // Partial root match: check each word in the field shares 4+ char prefix with term
        if (term.Length >= 4)
        {
            var prefix = term[..4];
            foreach (var word in lower.Split(' '))
                if (word.Length >= 4 && word.StartsWith(prefix))
                    return weight * 0.6;
        }
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

    // Strip diacritics and lowercase so "adoracao" matches "adoração", "fe" matches "fé"
    private static string Normalize(string s)
    {
        var formed = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(formed.Length);
        foreach (var c in formed)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        return sb.ToString().ToLowerInvariant();
    }
}
