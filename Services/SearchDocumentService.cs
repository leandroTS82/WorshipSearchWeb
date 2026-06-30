using System.Text.Json;
using WorshipSearchWeb.Models;

namespace WorshipSearchWeb.Services;

public interface ISearchDocumentService
{
    Task SaveAsync(SearchDocument doc);
}

public class SearchDocumentService : ISearchDocumentService
{
    private readonly string _folder;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public SearchDocumentService(IWebHostEnvironment env)
    {
        _folder = Path.Combine(env.ContentRootPath, "json", "searches");
        Directory.CreateDirectory(_folder);
    }

    public async Task SaveAsync(SearchDocument doc)
    {
        var path = Path.Combine(_folder, $"search-{doc.Id}.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(doc, JsonOpts));
    }
}
