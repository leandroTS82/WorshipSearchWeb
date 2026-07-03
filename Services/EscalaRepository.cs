using System.Text.Json;
using WorshipSearchWeb.Models;

namespace WorshipSearchWeb.Services;

public interface IEscalaRepository
{
    Task<EscalaDocument?> LoadAsync(string id);
    Task SaveAsync(EscalaDocument doc);
    Task DeleteAsync(string id);
    Task<List<EscalaDocument>> LoadAllAsync();
    Task<List<string>> GetMinistrosAsync();
    Task AddMinistroAsync(string nome);
    Task RemoveMinistroAsync(string nome);
    string NewId();
}

public class EscalaRepository : IEscalaRepository
{
    private readonly string _jsonFolder;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public EscalaRepository(IWebHostEnvironment env)
    {
        var rootJson = Path.Combine(env.ContentRootPath, "json");
        Directory.CreateDirectory(rootJson);
        _jsonFolder = Path.Combine(rootJson, "escalas");
        Directory.CreateDirectory(_jsonFolder);

        // one-time migration: move old escala-*.json files from json/ to json/escalas/
        foreach (var old in Directory.GetFiles(rootJson, "escala-*.json"))
        {
            var dest = Path.Combine(_jsonFolder, Path.GetFileName(old));
            if (!File.Exists(dest)) File.Move(old, dest);
            else File.Delete(old);
        }
    }

    public string NewId() =>
        DateTime.UtcNow.ToString("yyyyMMdd") + "-" + Guid.NewGuid().ToString("N")[..6];

    private string FilePath(string id) =>
        Path.Combine(_jsonFolder, $"escala-{id}.json");

    private string MinistrosPath =>
        Path.Combine(_jsonFolder, "ministros.json");

    public async Task<EscalaDocument?> LoadAsync(string id)
    {
        var path = FilePath(id);
        if (!File.Exists(path)) return null;
        var json = await File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize<EscalaDocument>(json, JsonOpts);
    }

    public async Task SaveAsync(EscalaDocument doc)
    {
        if (string.IsNullOrEmpty(doc.Id)) doc.Id = NewId();
        doc.AtualizadoEm = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        if (string.IsNullOrEmpty(doc.CriadoEm)) doc.CriadoEm = doc.AtualizadoEm;
        await File.WriteAllTextAsync(FilePath(doc.Id), JsonSerializer.Serialize(doc, JsonOpts));
    }

    public Task DeleteAsync(string id)
    {
        var path = FilePath(id);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    public async Task<List<EscalaDocument>> LoadAllAsync()
    {
        var files = Directory.GetFiles(_jsonFolder, "escala-*.json");
        var result = new List<EscalaDocument>();
        foreach (var file in files)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var doc = JsonSerializer.Deserialize<EscalaDocument>(json, JsonOpts);
                if (doc != null) result.Add(doc);
            }
            catch { /* skip corrupt */ }
        }
        return result
            .OrderByDescending(d => d.Data)
            .ThenByDescending(d => d.CriadoEm)
            .ToList();
    }

    public async Task<List<string>> GetMinistrosAsync()
    {
        if (!File.Exists(MinistrosPath)) return new();
        var json = await File.ReadAllTextAsync(MinistrosPath);
        return JsonSerializer.Deserialize<List<string>>(json) ?? new();
    }

    public async Task AddMinistroAsync(string nome)
    {
        var list = await GetMinistrosAsync();
        if (list.Any(m => string.Equals(m, nome, StringComparison.OrdinalIgnoreCase))) return;
        list.Add(nome);
        list.Sort(StringComparer.CurrentCultureIgnoreCase);
        await File.WriteAllTextAsync(MinistrosPath, JsonSerializer.Serialize(list, JsonOpts));
    }

    public async Task RemoveMinistroAsync(string nome)
    {
        var list = await GetMinistrosAsync();
        list.RemoveAll(m => string.Equals(m, nome, StringComparison.OrdinalIgnoreCase));
        await File.WriteAllTextAsync(MinistrosPath, JsonSerializer.Serialize(list, JsonOpts));
    }
}
