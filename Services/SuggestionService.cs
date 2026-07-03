namespace WorshipSearchWeb.Services;

public interface ISuggestionService
{
    Task<string[]> GetSuggestionsAsync();
    void Invalidate();
}

public class SuggestionService : ISuggestionService
{
    private readonly IMusicRepository _repository;
    private readonly IAdditionalSuggestionsService _additionalSuggestions;
    private readonly ILogger<SuggestionService> _logger;
    private volatile string[]? _cache;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public SuggestionService(IMusicRepository repository, IAdditionalSuggestionsService additionalSuggestions, ILogger<SuggestionService> logger)
    {
        _repository            = repository;
        _additionalSuggestions = additionalSuggestions;
        _logger                = logger;
    }

    public async Task<string[]> GetSuggestionsAsync()
    {
        if (_cache is { } hit) return hit;

        await _lock.WaitAsync();
        try
        {
            if (_cache is { } cached) return cached;
            _cache = await BuildAsync();
            return _cache;
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Invalidate() => _cache = null;

    private async Task<string[]> BuildAsync()
    {
        try
        {
            var all = await _repository.LoadAllAsync();
            var additional = (await _additionalSuggestions.LoadAsync()).Items
                .Select(x => x.ToLowerInvariant())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var freq = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            void Count(IEnumerable<string>? list, int weight = 1)
            {
                if (list == null) return;
                foreach (var item in list)
                    if (!string.IsNullOrWhiteSpace(item) && item.Length <= 40)
                        freq[item] = freq.GetValueOrDefault(item, 0) + weight;
            }

            foreach (var (_, doc) in all)
            {
                var s = doc.Search;
                if (s == null) continue;

                Count(s.Themes,         weight: 3);
                Count(s.WorshipMoments, weight: 3);
                Count(s.Moods,          weight: 2);
                Count(s.Contexts,       weight: 2);
                Count(s.BiblicalTopics, weight: 2);
                Count(s.Keywords,       weight: 1);
                Count(s.Synonyms,       weight: 1);

                // Only short biblical references (e.g. "Salmo 91", "João 3:16")
                foreach (var r in s.BiblicalReferences ?? [])
                    if (!string.IsNullOrWhiteSpace(r) && r.Length <= 20)
                        freq[r] = freq.GetValueOrDefault(r, 0) + 2;
            }

            _logger.LogInformation("SuggestionService: built {Count} suggestions from {Songs} songs",
                freq.Count, all.Count);

            return freq
                .Where(x => !additional.Contains(x.Key))
                .OrderByDescending(x => x.Value)
                .ThenBy(x => x.Key)
                .Take(60)
                .Select(x => x.Key)
                .ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SuggestionService: failed to build suggestions");
            return [];
        }
    }
}
