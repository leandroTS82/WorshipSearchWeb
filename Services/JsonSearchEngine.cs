using System.Globalization;
using System.Text;
using WorshipSearchWeb.Models;

namespace WorshipSearchWeb.Services;

public interface IJsonSearchEngine
{
    Task<List<LibrarySearchResult>> SearchAsync(SearchQuery query);
    Task<int> CountAsync(SearchQuery query);
    Task<List<LibrarySearchResult>> SearchByDocumentAsync(List<SearchCriterion> criteria);
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
        var all = await BuildFilteredAsync(query);
        return all
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();
    }

    public async Task<int> CountAsync(SearchQuery query)
    {
        var all = await BuildFilteredAsync(query);
        return all.Count;
    }

    private async Task<List<LibrarySearchResult>> BuildFilteredAsync(SearchQuery query)
    {
        var all = await _repository.LoadAllAsync();
        var restrictions = await _restrictions.LoadAsync();
        var excluded = restrictions.Excluded
            .Select(e => Normalize(e))
            .ToHashSet();

        var terms = string.IsNullOrWhiteSpace(query.Query)
            ? Array.Empty<string>()
            : query.Query
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(Normalize)
                .Where(t => t.Length > 1 && !StopWords.Contains(t))
                .Distinct()
                .ToArray();

        // Full normalized query used for exact title match — bypasses stop-word filtering.
        // "É ele" → "e ele" which equals Normalize("É Ele"), guaranteeing exact title lookup wins.
        var normRawQuery = Normalize(query.Query ?? string.Empty);

        var results = new List<LibrarySearchResult>();

        foreach (var (fileName, doc) in all)
        {
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
                (score, matchedTerms) = ScoreDocument(doc, terms, normRawQuery);
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
            .ToList();
    }

    public async Task<List<LibrarySearchResult>> SearchByDocumentAsync(List<SearchCriterion> criteria)
    {
        var all = await _repository.LoadAllAsync();
        var restrictions = await _restrictions.LoadAsync();
        var excluded = restrictions.Excluded.Select(e => Normalize(e)).ToHashSet();

        var results = new List<LibrarySearchResult>();

        foreach (var (fileName, doc) in all)
        {
            if (excluded.Count > 0 && IsExcluded(doc, excluded)) continue;

            double totalScore = 0;
            var matchedCriteria = new List<string>();

            foreach (var criterion in criteria)
            {
                var (score, matched) = ScoreByCriterion(doc, criterion);
                if (matched) matchedCriteria.Add(criterion.Input);
                totalScore += score;
            }

            if (matchedCriteria.Count > 0)
            {
                results.Add(new LibrarySearchResult
                {
                    Document         = doc,
                    FileName         = fileName,
                    Score            = totalScore,
                    MatchedTerms     = matchedCriteria.Count,
                    MatchedCriteria  = matchedCriteria
                });
            }
        }

        return results
            .OrderByDescending(r => r.MatchedTerms)
            .ThenByDescending(r => r.Score)
            .ToList();
    }

    private static (double score, bool matched) ScoreByCriterion(MusicDocument doc, SearchCriterion criterion)
    {
        var search = doc.Search ?? new SearchMetadata();
        var tags   = criterion.Tags ?? new SearchMetadata();
        double score = 0;

        switch (criterion.Type)
        {
            case "biblical_reference":
                score += ScoreListAgainstList(search.BiblicalReferences, tags.BiblicalReferences, 10);
                score += ScoreListAgainstList(search.BiblicalBooks,      tags.BiblicalBooks,      8);
                score += ScoreListAgainstList(search.BiblicalTopics,     tags.BiblicalTopics,     7);
                score += ScoreListAgainstList(search.BiblicalCharacters, tags.BiblicalCharacters, 4);
                score += ScoreField(doc.Summary, Normalize(criterion.Input), 2);
                score += ScoreField(doc.Lyrics,  Normalize(criterion.Input), 1);
                break;

            case "worship_moment":
                score += ScoreListAgainstList(search.WorshipMoments, tags.WorshipMoments, 10);
                score += ScoreListAgainstList(search.Contexts,        tags.Contexts,       8);
                if (!string.IsNullOrEmpty(tags.Occasion))
                    score += ScoreField(search.Occasion, Normalize(tags.Occasion), 7);
                score += ScoreListAgainstList(search.Themes, tags.Themes, 4);
                score += ScoreField(doc.Summary, Normalize(criterion.Input), 2);
                break;

            case "mood":
                score += ScoreListAgainstList(search.Moods, tags.Moods, 10);
                if (!string.IsNullOrEmpty(tags.EnergyLevel))
                    score += ScoreField(search.EnergyLevel, Normalize(tags.EnergyLevel), 8);
                if (!string.IsNullOrEmpty(tags.WorshipStyle))
                    score += ScoreField(search.WorshipStyle, Normalize(tags.WorshipStyle), 7);
                score += ScoreListAgainstList(search.Themes,   tags.Themes,   5);
                score += ScoreListAgainstList(search.Keywords, tags.Keywords, 3);
                break;

            case "context":
                score += ScoreListAgainstList(search.Contexts,      tags.Contexts,      10);
                score += ScoreListAgainstList(search.WorshipMoments,tags.WorshipMoments, 8);
                score += ScoreListAgainstList(search.Themes,        tags.Themes,         4);
                score += ScoreField(doc.Summary, Normalize(criterion.Input), 2);
                break;

            default: // "theme", "keyword", "general"
                score += ScoreListAgainstList(search.Themes,       tags.Themes,          10);
                score += ScoreListAgainstList(search.Keywords,     tags.Keywords,         8);
                score += ScoreListAgainstList(search.Synonyms,     tags.Synonyms,         6);
                score += ScoreListAgainstList(search.BiblicalTopics, tags.BiblicalTopics, 5);
                score += ScoreListAgainstList(search.Contexts,     tags.Contexts,         4);
                score += ScoreListAgainstList(search.Moods,        tags.Moods,            3);
                var norm = Normalize(criterion.Input);
                if (norm.Length > 1)
                {
                    score += ScoreField(doc.Title,   norm, 5);
                    score += ScoreField(doc.Artist,  norm, 4);
                    score += ScoreField(doc.Summary, norm, 3);
                    score += ScoreField(doc.Lyrics,  norm, 1);
                }
                break;
        }

        return (score, score > 0);
    }

    private static double ScoreListAgainstList(List<string>? docList, List<string>? searchTags, double weight)
    {
        if (docList == null || searchTags == null || searchTags.Count == 0) return 0;
        double score = 0;
        foreach (var tag in searchTags)
        {
            var normTag = Normalize(tag);
            if (normTag.Length < 2) continue;
            foreach (var docItem in docList)
                score += ScoreField(docItem, normTag, weight);
        }
        return score;
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

    private static (double score, int matchedTerms) ScoreDocument(MusicDocument doc, string[] terms, string normRawQuery = "")
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

        // Title-priority: float songs to the top when the query targets a specific title.
        total += TitlePriorityBonus(doc.Title, terms, normRawQuery);

        return (total, matchedTerms);
    }

    private static double TitlePriorityBonus(string title, string[] terms, string normRawQuery = "")
    {
        var normTitle = Normalize(title);

        // Strongest: full normalized query equals normalized title — direct title lookup.
        // "É ele" → "e ele" == Normalize("É Ele") → guaranteed first position.
        if (!string.IsNullOrWhiteSpace(normRawQuery) && normTitle == normRawQuery)
            return 2000;

        if (terms.Length == 0) return 0;
        var titleWords = normTitle.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // Strong: all query terms appear in the title
        if (terms.All(t => normTitle.Contains(t))) return 1000;

        // Moderate: all terms match title words within edit-distance 1 (typo tolerance)
        bool allFuzzy = terms.All(t =>
            normTitle.Contains(t) ||
            (t.Length >= 5 && titleWords.Any(w =>
                Math.Abs(w.Length - t.Length) <= 2 && EditDistance(w, t) <= 1)));
        return allFuzzy ? 500 : 0;
    }

    private static int EditDistance(string a, string b)
    {
        int m = a.Length, n = b.Length;
        var d = new int[m + 1, n + 1];
        for (int i = 0; i <= m; i++) d[i, 0] = i;
        for (int j = 0; j <= n; j++) d[0, j] = j;
        for (int i = 1; i <= m; i++)
            for (int j = 1; j <= n; j++)
                d[i, j] = a[i - 1] == b[j - 1]
                    ? d[i - 1, j - 1]
                    : 1 + Math.Min(d[i - 1, j], Math.Min(d[i, j - 1], d[i - 1, j - 1]));
        return d[m, n];
    }

    private static double ScoreField(string field, string term, double weight)
    {
        if (string.IsNullOrWhiteSpace(field)) return 0;
        var lower = Normalize(field);
        if (lower == term) return weight * 2;
        if (lower.Contains(term)) return weight;
        // Prefix and fuzzy matching only for single-word terms.
        // Multi-word terms (e.g. "isaias 41:10") must match exactly/by substring to avoid
        // false positives like "Isaías 6" matching the 4-char prefix "isai".
        if (term.Contains(' ')) return 0;
        // Prefix match: 4+ char shared root
        if (term.Length >= 4)
        {
            var prefix = term[..4];
            foreach (var word in lower.Split(' '))
                if (word.Length >= 4 && word.StartsWith(prefix))
                    return weight * 0.6;
        }
        // Fuzzy match: single character typo (edit distance ≤ 1) for terms ≥ 5 chars
        if (term.Length >= 5)
        {
            foreach (var word in lower.Split(' '))
                if (word.Length >= 4 && Math.Abs(word.Length - term.Length) <= 2 && EditDistance(word, term) <= 1)
                    return weight * 0.4;
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
