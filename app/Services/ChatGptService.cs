using System.Net.Http.Json;
using GoodDeeds.Client.Models;

namespace GoodDeeds.Client.Services;

public record ChatGptSuggestion(int Points, string Reason);

public class ChatGptService
{
    private readonly HttpClient _http;

    public ChatGptService(HttpClient http)
    {
        _http = http;
    }

    public async Task<ChatGptSuggestion?> SuggestPointsForConditionAsync(string deedTypeName, string? condition, bool isPositive, IReadOnlyList<DeedTypeDto> deedTypes, CancellationToken cancellationToken = default)
    {
        var payload = new AiSuggestRequest(
            deedTypeName,
            string.IsNullOrWhiteSpace(condition) ? null : condition.Trim(),
            isPositive,
            deedTypes.Select(d => new AiDeedType(d.Name, d.Points)).ToArray());
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

    private sealed record AiSuggestRequest(string DeedTypeName, string? Condition, bool IsPositive, AiDeedType[] DeedTypes);
    private sealed record AiDeedType(string Name, int Points);
    private sealed record ErrorResponse(string Error);
}
