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
    };

    public async Task<string> RequestAsync(string prompt, CancellationToken cancellationToken = default)
    {
        var requestBody = GemmaRequestFactory.CreateRequest(prompt);
        var content = new StringContent(JsonConvert.SerializeObject(requestBody, Formatting.None, _serializerSettings), Encoding.UTF8, "application/json");

        using var response = await _httpClient.PostAsync("", content, cancellationToken);

        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadAsStringAsync();

        var deserializedResponse = JsonConvert.DeserializeObject<GemmaResponse>(responseBody);
        var responseText = deserializedResponse?.Candidates?
            .SelectMany(candidate => candidate.Content?.Parts ?? [])
            .Select(part => part.Text)
            .FirstOrDefault(text => !string.IsNullOrWhiteSpace(text));

        return responseText ?? throw new InvalidOperationException("Gemma returned no text candidate.");
    }
}
