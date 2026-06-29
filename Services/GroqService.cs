using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace WorshipSearchWeb.Services;

public interface IGroqService
{
    Task<string> CompleteAsync(string prompt, string model = "openai/gpt-oss-120b");
}

public class GroqService : IGroqService
{
    private readonly HttpClient _httpClient;
    private readonly ILlmKeyService _llmKeyService;
    private readonly ILogger<GroqService> _logger;

    private const string GroqEndpoint = "https://api.groq.com/openai/v1/chat/completions";

    public GroqService(HttpClient httpClient, ILlmKeyService llmKeyService, ILogger<GroqService> logger)
    {
        _httpClient = httpClient;
        _llmKeyService = llmKeyService;
        _logger = logger;
    }

    public async Task<string> CompleteAsync(string prompt, string model = "openai/gpt-oss-120b")
    {
        var apiKey = await _llmKeyService.GetGroqKeyAsync();

        var requestBody = new
        {
            model,
            messages = new[]
            {
                new { role = "user", content = prompt }
            },
            temperature = 0.2,
            max_tokens = 4096
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var request = new HttpRequestMessage(HttpMethod.Post, GroqEndpoint)
        {
            Content = content
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var response = await _httpClient.SendAsync(request);
        var responseText = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Groq API error {Status}: {Body}", response.StatusCode, responseText);
            throw new InvalidOperationException($"Groq API returned {response.StatusCode}: {responseText}");
        }

        var doc = JsonDocument.Parse(responseText);
        var message = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        return message ?? string.Empty;
    }
}
