using System.Text.Json;
using WorshipSearchWeb.Models;
using WorshipSearchWeb.Services;

namespace WorshipSearchWeb.Services;

public static class MusicStatusMigrationService
{
    private const string FlagV1 = ".migration-v1-approved";
    private const string FlagV2 = ".migration-v2-remove-celebracao";
    private const string FlagV3 = ".migration-v3-strip-letras-suffix";

    private static readonly JsonSerializerOptions _opts = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static async Task RunIfNeededAsync(string jsonFolder, ILogger logger)
    {
        await MigrateV1ApprovedAsync(jsonFolder, logger);
        await MigrateV2RemoveCelebracaoAsync(jsonFolder, logger);
        await MigrateV3StripLetrasSuffixAsync(jsonFolder, logger);
    }

    private static async Task MigrateV1ApprovedAsync(string jsonFolder, ILogger logger)
    {
        var flagPath = Path.Combine(jsonFolder, FlagV1);
        if (File.Exists(flagPath)) return;

        logger.LogInformation("MusicStatusMigration v1: marking all existing songs as approved...");
        var files = Directory.GetFiles(jsonFolder, "louvor-*.json");
        int count = 0;
        foreach (var file in files)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var doc = JsonSerializer.Deserialize<MusicDocument>(json);
                if (doc == null) continue;
                doc.Status = "approved";
                await File.WriteAllTextAsync(file, JsonSerializer.Serialize(doc, _opts));
                count++;
            }
            catch (Exception ex) { logger.LogWarning(ex, "MusicStatusMigration v1: failed {File}", file); }
        }
        await File.WriteAllTextAsync(flagPath, DateTime.UtcNow.ToString("O"));
        logger.LogInformation("MusicStatusMigration v1: {Count} songs marked as approved.", count);
    }

    private static async Task MigrateV2RemoveCelebracaoAsync(string jsonFolder, ILogger logger)
    {
        var flagPath = Path.Combine(jsonFolder, FlagV2);
        if (File.Exists(flagPath)) return;

        logger.LogInformation("MusicStatusMigration v2: removing 'celebração' tag from all songs...");
        var files = Directory.GetFiles(jsonFolder, "louvor-*.json");
        int count = 0;
        foreach (var file in files)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var doc = JsonSerializer.Deserialize<MusicDocument>(json);
                if (doc?.Search == null) continue;

                bool changed = false;
                changed |= RemoveCelebracao(doc.Search.Themes);
                changed |= RemoveCelebracao(doc.Search.Keywords);
                changed |= RemoveCelebracao(doc.Search.Contexts);
                changed |= RemoveCelebracao(doc.Search.Moods);
                changed |= RemoveCelebracao(doc.Search.Synonyms);
                changed |= RemoveCelebracao(doc.Search.WorshipMoments);

                if (changed)
                {
                    await File.WriteAllTextAsync(file, JsonSerializer.Serialize(doc, _opts));
                    count++;
                }
            }
            catch (Exception ex) { logger.LogWarning(ex, "MusicStatusMigration v2: failed {File}", file); }
        }
        await File.WriteAllTextAsync(flagPath, DateTime.UtcNow.ToString("O"));
        logger.LogInformation("MusicStatusMigration v2: removed 'celebração' from {Count} songs.", count);
    }

    private static async Task MigrateV3StripLetrasSuffixAsync(string jsonFolder, ILogger logger)
    {
        var flagPath = Path.Combine(jsonFolder, FlagV3);
        if (File.Exists(flagPath)) return;

        logger.LogInformation("MusicStatusMigration v3: stripping ' - LETRAS.MUS.BR' from artist names...");
        var files = Directory.GetFiles(jsonFolder, "louvor-*.json");
        int count = 0;
        foreach (var file in files)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var doc = JsonSerializer.Deserialize<MusicDocument>(json);
                if (doc == null) continue;

                var clean = MusicHtmlParser.StripLetrasSuffix(doc.Artist ?? "");
                if (clean == doc.Artist) continue;

                doc.Artist = clean;
                await File.WriteAllTextAsync(file, JsonSerializer.Serialize(doc, _opts));
                count++;
            }
            catch (Exception ex) { logger.LogWarning(ex, "MusicStatusMigration v3: failed {File}", file); }
        }
        await File.WriteAllTextAsync(flagPath, DateTime.UtcNow.ToString("O"));
        logger.LogInformation("MusicStatusMigration v3: cleaned artist on {Count} songs.", count);
    }

    private static bool RemoveCelebracao(List<string>? list)
    {
        if (list == null) return false;
        var before = list.Count;
        list.RemoveAll(t => t.Equals("celebração", StringComparison.OrdinalIgnoreCase)
                         || t.Equals("celebracao",  StringComparison.OrdinalIgnoreCase)
                         || t.Equals("celebracao",  StringComparison.OrdinalIgnoreCase));
        return list.Count != before;
    }
}
