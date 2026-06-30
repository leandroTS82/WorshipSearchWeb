using System.Text.Json;
using System.Text.Encodings.Web;

namespace WorshipSearchWeb.Services;

public interface IAppLogger
{
    Task InfoAsync(string eventName, object? data = null);
    Task WarnAsync(string eventName, object? data = null);
    Task ErrorAsync(string eventName, Exception? ex = null, object? data = null);
}

public class FileAppLogger : IAppLogger
{
    private readonly string _logPath;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public FileAppLogger(IWebHostEnvironment env)
    {
        var logDir = Path.Combine(env.ContentRootPath, "logs");
        Directory.CreateDirectory(logDir);
        _logPath = Path.Combine(logDir, "app.jsonl");
    }

    public Task InfoAsync(string eventName, object? data = null)  => WriteAsync("info",  eventName, null, data);
    public Task WarnAsync(string eventName, object? data = null)  => WriteAsync("warn",  eventName, null, data);
    public Task ErrorAsync(string eventName, Exception? ex = null, object? data = null)
        => WriteAsync("error", eventName, ex, data);

    private async Task WriteAsync(string level, string eventName, Exception? ex, object? data)
    {
        try
        {
            var entry = new
            {
                ts    = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                level,
                evt   = eventName,
                error = ex?.Message,
                data
            };
            var line = JsonSerializer.Serialize(entry, JsonOpts) + "\n";
            await _lock.WaitAsync();
            try   { await File.AppendAllTextAsync(_logPath, line); }
            finally { _lock.Release(); }
        }
        catch { /* logger must never throw */ }
    }
}
