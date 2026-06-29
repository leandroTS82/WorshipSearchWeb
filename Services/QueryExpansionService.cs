using System.Text.Json;
using WorshipSearchWeb.Prompts;

namespace WorshipSearchWeb.Services;

public interface IQueryExpansionService
{
    Task<(string? ExpandedQuery, string? Description)> ExpandAsync(string query);
}

public class QueryExpansionService : IQueryExpansionService
{
    private readonly IGroqService _groq;
    private readonly ILogger<QueryExpansionService> _logger;

    public QueryExpansionService(IGroqService groq, ILogger<QueryExpansionService> logger)
    {
        _groq = groq;
        _logger = logger;
    }

    public async Task<(string? ExpandedQuery, string? Description)> ExpandAsync(string query)
    {
        try
        {
            var criteriaList = query
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(c => c.Trim())
                .Where(c => c.Length > 0)
                .ToArray();

            if (criteriaList.Length == 0) return (null, null);

            var numberedList = string.Join("\n", criteriaList.Select((c, i) => $"{i + 1}. {c}"));
            var prompt = PromptTemplates.QueryExpansionTemplate.Replace("{criteria_list}", numberedList);
            var response = await _groq.CompleteAsync(prompt);

            var trimmed = response.Trim();
            var start = trimmed.IndexOf('{');
            var end = trimmed.LastIndexOf('}');
            if (start < 0 || end <= start) return (null, null);

            var jsonObj = trimmed[start..(end + 1)];
            using var doc = JsonDocument.Parse(jsonObj);
            var root = doc.RootElement;

            var description = root.TryGetProperty("description", out var descProp)
                ? descProp.GetString()
                : null;

            if (!root.TryGetProperty("criteria", out var criteriaArr) || criteriaArr.ValueKind != JsonValueKind.Array)
                return (null, description);

            var expandedParts = new List<string>();
            int idx = 0;
            foreach (var criterionEl in criteriaArr.EnumerateArray())
            {
                var original = idx < criteriaList.Length ? criteriaList[idx] : "";
                var newTerms = new List<string>();
                if (criterionEl.TryGetProperty("terms", out var termsProp) && termsProp.ValueKind == JsonValueKind.Array)
                    foreach (var t in termsProp.EnumerateArray())
                    {
                        var ts = t.GetString();
                        if (!string.IsNullOrWhiteSpace(ts)) newTerms.Add(ts.Trim());
                    }

                var part = string.Join(" ", new[] { original }.Concat(newTerms.Take(5)));
                expandedParts.Add(part);
                idx++;
            }

            if (expandedParts.Count == 0) return (null, description);

            // Carry over any criteria the LLM didn't address
            for (; idx < criteriaList.Length; idx++)
                expandedParts.Add(criteriaList[idx]);

            return (string.Join(";", expandedParts), description);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LLM query expansion failed for: {Query}", query);
            return (null, null);
        }
    }
}
