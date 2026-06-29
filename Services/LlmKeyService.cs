using System.Text.Json;

namespace WorshipSearchWeb.Services;

public interface ILlmKeyService
{
    Task<string> GetGroqKeyAsync();
}

public class LlmKeyService : ILlmKeyService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<LlmKeyService> _logger;

    private const string KeyEndpoint = "http://llmkeys.twobrothermotors.com.br/api/keys/groq/next";
    private const string BearerToken = "llmkey-super-secret-bearer-2025";

    public LlmKeyService(HttpClient httpClient, ILogger<LlmKeyService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<string> GetGroqKeyAsync()
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, KeyEndpoint);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", BearerToken);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var json = JsonDocument.Parse(content);

            // Try common key field names
            if (json.RootElement.TryGetProperty("key", out var keyProp))
                return keyProp.GetString() ?? throw new InvalidOperationException("Empty key returned");
            if (json.RootElement.TryGetProperty("api_key", out var apiKeyProp))
                return apiKeyProp.GetString() ?? throw new InvalidOperationException("Empty key returned");
            if (json.RootElement.TryGetProperty("value", out var valueProp))
                return valueProp.GetString() ?? throw new InvalidOperationException("Empty key returned");

            // If root is a plain string
            if (json.RootElement.ValueKind == JsonValueKind.String)
                return json.RootElement.GetString() ?? throw new InvalidOperationException("Empty key returned");

            throw new InvalidOperationException($"Cannot parse key from response: {content}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch Groq API key");
            throw;
        }
    }
}
