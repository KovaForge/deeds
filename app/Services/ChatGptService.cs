using System.Net.Http.Json;

namespace GoodDeeds.Client.Services;

public record ChatGptSuggestion(int Points, string Reason);

public class ChatGptService
{
    private readonly HttpClient _http;

    public ChatGptService(HttpClient http)
    {
        _http = http;
    }

    public async Task<ChatGptSuggestion?> SuggestPointsForConditionAsync(string deedTypeName, string condition, bool isPositive, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(condition))
        {
            throw new ArgumentException("Condition is required", nameof(condition));
        }

        var payload = new AiSuggestRequest(deedTypeName, condition, isPositive);
        using var response = await _http.PostAsJsonAsync("ai/suggest", payload, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await ReadErrorAsync(response, cancellationToken);
            var message = error ?? $"AI suggestion failed (Status: {(int)response.StatusCode} {response.StatusCode})";
            throw new InvalidOperationException(message);
        }

        return await response.Content.ReadFromJsonAsync<ChatGptSuggestion>(cancellationToken);
    }

    private static async Task<string?> ReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(cancellationToken: cancellationToken);
            return error?.Error;
        }
        catch
        {
            return null;
        }
    }

    private sealed record AiSuggestRequest(string DeedTypeName, string Condition, bool IsPositive);
    private sealed record ErrorResponse(string Error);
}
