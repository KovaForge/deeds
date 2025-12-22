using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace GoodDeeds.Client.Services;

public record ChatGptSuggestion(int Points, string Reason);

public class ChatGptService
{
    private static readonly Uri OpenAiBaseUri = new("https://api.openai.com/");

    public async Task<ChatGptSuggestion?> SuggestPointsAsync(string apiKey, string deedTypeName, bool isPositive, string description, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException("API key is required", nameof(apiKey));
        }

        using var httpClient = new HttpClient { BaseAddress = OpenAiBaseUri };
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var requestBody = new
        {
            model = "gpt-4o-mini",
            temperature = 0.3,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new { role = "system", content = "You help parents score children's deeds. Respond with JSON containing integer 'points' and string 'reason'. Good deeds should have positive points, bad deeds negative." },
                new { role = "user", content = $"Deed type: {deedTypeName}. Nature: {(isPositive ? "good" : "bad")}. Description: {description}." }
            }
        };

        using var response = await httpClient.PostAsJsonAsync("v1/chat/completions", requestBody, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (!document.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
        {
            return null;
        }

        var content = choices[0].GetProperty("message").GetProperty("content").GetString();
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        using var suggestionJson = JsonDocument.Parse(content);
        var points = suggestionJson.RootElement.TryGetProperty("points", out var pointsProp) ? pointsProp.GetInt32() : (int?)null;
        var reason = suggestionJson.RootElement.TryGetProperty("reason", out var reasonProp) ? reasonProp.GetString() : null;

        if (points is null || reason is null)
        {
            return null;
        }

        return new ChatGptSuggestion(points.Value, reason);
    }

    public async Task<ChatGptSuggestion?> SuggestPointsForConditionAsync(string apiKey, string condition, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException("API key is required", nameof(apiKey));
        }

        if (string.IsNullOrWhiteSpace(condition))
        {
            throw new ArgumentException("Condition is required", nameof(condition));
        }

        using var httpClient = new HttpClient { BaseAddress = OpenAiBaseUri };
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var systemPrompt = @"You are a parents' helper for managing children's behavior tracking via a reward/consequence system based on screen time minutes.

Your job is to assess behaviors and assign points representing minutes to be added or deducted from screen time.

Use these guidelines:
- Mild Positive (routine good behavior): +5 to +10 minutes
- Moderate Positive (good effort/achievement): +10 to +20 minutes
- Significant Positive (rare/important achievement): +50 minutes
- Mild Negative (minor misbehavior): -5 to -10 minutes
- Moderate Negative (disruptive behavior): -10 to -30 minutes
- Severe Negative (dishonesty, aggression, serious misconduct): -300 to -500 minutes

Consider:
- Rarity: Rare achievements get scaled rewards
- Frequency: Common behaviors get adjusted deductions
- Severity: How serious is the behavior?
- Impact: How much does it affect the family?

Respond with JSON containing:
- points: integer (positive for good, negative for bad)
- reason: string (brief explanation of your assessment)";

        var requestBody = new
        {
            model = "gpt-4o-mini",
            temperature = 0.5,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = $"Assess this behavior and suggest screen time minutes:\n\n{condition}" }
            }
        };

        using var response = await httpClient.PostAsJsonAsync("v1/chat/completions", requestBody, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (!document.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
        {
            return null;
        }

        var content = choices[0].GetProperty("message").GetProperty("content").GetString();
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        using var suggestionJson = JsonDocument.Parse(content);
        var points = suggestionJson.RootElement.TryGetProperty("points", out var pointsProp) ? pointsProp.GetInt32() : (int?)null;
        var reason = suggestionJson.RootElement.TryGetProperty("reason", out var reasonProp) ? reasonProp.GetString() : null;

        if (points is null || reason is null)
        {
            return null;
        }

        return new ChatGptSuggestion(points.Value, reason);
    }
}
