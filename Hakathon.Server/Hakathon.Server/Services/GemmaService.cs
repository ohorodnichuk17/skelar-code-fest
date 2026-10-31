using Hakathon.Server.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Text;

namespace Hakathon.Server.Services;

public class GemmaService(HttpClient httpClient)
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly JsonSerializerSettings _serializerSettings = new JsonSerializerSettings()
    {
        ContractResolver = new DefaultContractResolver()
        {
            NamingStrategy = new SnakeCaseNamingStrategy()
        },
        NullValueHandling = NullValueHandling.Ignore,
    };

    private static readonly GemmaGenerationConfig JsonConfig = new()
    {
        Temperature = 0,
        ResponseMimeType = "application/json",
        ThinkingConfig = new GemmaThinkingConfig { ThinkingLevel = "minimal" }
    };

    public async Task<string> RequestAsync(string prompt, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(GemmaRequestFactory.CreateRequest(prompt), cancellationToken);

        response.EnsureSuccessStatusCode();

        return await ReadTextAsync(response, cancellationToken)
            ?? throw new InvalidOperationException("Gemma returned no text candidate.");
    }

    public async Task<string> GenerateJsonAsync(string prompt, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(GemmaRequestFactory.CreateRequest(prompt, JsonConfig), cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new GemmaProviderException($"http_{(int)response.StatusCode}");
        }

        return await ReadTextAsync(response, cancellationToken) ?? throw new GemmaProviderException("empty_response");
    }

    private Task<HttpResponseMessage> SendAsync(GemmaRequest request, CancellationToken cancellationToken)
    {
        var content = new StringContent(JsonConvert.SerializeObject(request, Formatting.None, _serializerSettings), Encoding.UTF8, "application/json");
        return _httpClient.PostAsync("", content, cancellationToken);
    }

    private static async Task<string?> ReadTextAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        var deserializedResponse = JsonConvert.DeserializeObject<GemmaResponse>(responseBody);
        return deserializedResponse?.Candidates?
            .SelectMany(candidate => candidate.Content?.Parts ?? [])
            .Where(part => part.Thought != true)
            .Select(part => part.Text)
            .LastOrDefault(text => !string.IsNullOrWhiteSpace(text));
    }
}

public sealed class GemmaProviderException(string reason) : Exception(reason)
{
    public string Reason { get; } = reason;
}

internal static class ModelJson
{
    public static readonly System.Text.Json.JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static string ExtractObject(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        return start >= 0 && end > start ? text[start..(end + 1)] : text;
    }
}
