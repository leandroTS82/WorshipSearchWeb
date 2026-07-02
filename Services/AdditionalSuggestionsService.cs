using System.Text.Json;
using WorshipSearchWeb.Models;

namespace WorshipSearchWeb.Services;

public interface IAdditionalSuggestionsService
{
    Task<AdditionalSuggestions> LoadAsync();
    Task SaveAsync(AdditionalSuggestions suggestions);
}

public class AdditionalSuggestionsService : IAdditionalSuggestionsService
{
    private readonly string _filePath;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public AdditionalSuggestionsService(IWebHostEnvironment env)
    {
        var jsonFolder = Path.Combine(env.ContentRootPath, "json");
        Directory.CreateDirectory(jsonFolder);
        _filePath = Path.Combine(jsonFolder, "additional-suggestions.json");
    }

    public async Task<AdditionalSuggestions> LoadAsync()
    {
        if (!File.Exists(_filePath)) return new AdditionalSuggestions();
        var json = await File.ReadAllTextAsync(_filePath);
        return JsonSerializer.Deserialize<AdditionalSuggestions>(json) ?? new AdditionalSuggestions();
    }

    public async Task SaveAsync(AdditionalSuggestions suggestions)
    {
        var json = JsonSerializer.Serialize(suggestions, JsonOpts);
        await File.WriteAllTextAsync(_filePath, json);
    }
}
