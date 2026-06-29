using System.Text.Json;
using WorshipSearchWeb.Prompts;

namespace WorshipSearchWeb.Services;

public interface IQueryExpansionService
{
    Task<string?> ExpandAsync(string query);
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

    public async Task<string?> ExpandAsync(string query)
    {
        try
        {
            var prompt = PromptTemplates.QueryExpansionTemplate.Replace("{query}", query);
            var response = await _groq.CompleteAsync(prompt);

            var trimmed = response.Trim();
            var start = trimmed.IndexOf('[');
            var end = trimmed.LastIndexOf(']');
            if (start < 0 || end <= start) return null;

            var jsonArray = trimmed[start..(end + 1)];
            var terms = JsonSerializer.Deserialize<string[]>(jsonArray);
            if (terms == null || terms.Length == 0) return null;

            var expanded = string.Join(" ", terms.Take(8).Where(t => !string.IsNullOrWhiteSpace(t)));
            return $"{query.Trim()} {expanded}".Trim();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LLM query expansion failed for: {Query}", query);
            return null;
        }
    }
}
