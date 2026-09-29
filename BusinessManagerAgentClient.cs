using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace PatarikAIOS;

public sealed class BusinessManagerAgentClient
{
    private readonly HttpClient _httpClient;

    public BusinessManagerAgentClient(string baseUrl = "http://127.0.0.1:8765")
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = TimeSpan.FromSeconds(60)
        };
    }

    public async Task<string> AskAsync(string message, DashboardSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            message,
            business_context = new
            {
                snapshot,
                recommendations = snapshot.Recommendations
            }
        };

        using var response = await _httpClient.PostAsJsonAsync("/agent/ask", payload, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"AI Manager service error ({(int)response.StatusCode}): {body}");
        }

        var result = await response.Content.ReadFromJsonAsync<AgentReply>(cancellationToken: cancellationToken);
        return result?.Answer?.Trim() ?? "AI Manager-ը պատասխան չվերադարձրեց։";
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync("/health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private sealed class AgentReply
    {
        public string? Answer { get; set; }
    }
}
