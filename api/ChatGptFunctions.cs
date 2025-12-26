using System.Net;
using System.Security.Cryptography;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

public class ChatGptFunctions
{
    private static readonly Uri OpenAiBaseUri = new("https://api.openai.com/");
    private static readonly HttpClient HttpClient = new() { BaseAddress = OpenAiBaseUri };
    private readonly string _cs;

    public ChatGptFunctions(DbOptions options)
    {
        _cs = options?.ConnectionString ?? string.Empty;
        if (string.IsNullOrWhiteSpace(_cs))
        {
            throw new InvalidOperationException("Database connection string (DB environment variable) is not configured. Set the DB environment variable in Azure Static Web Apps configuration.");
        }
    }

    [Function("SuggestPoints")]
    public async Task<HttpResponseData> SuggestPoints(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "ai/suggest")] HttpRequestData req)
    {
        if (!ParentGuard.TryGetParent(req, _cs, out var parentId, out var guardError))
        {
            return guardError;
        }

        AiSuggestRequest? payload;
        try
        {
            payload = await req.ReadFromJsonAsync<AiSuggestRequest>();
        }
        catch (JsonException)
        {
            return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "Invalid JSON payload");
        }

        if (payload is null)
        {
            return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "Request body required");
        }

        if (string.IsNullOrWhiteSpace(payload.DeedTypeName))
        {
            return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "DeedTypeName is required");
        }

        if (string.IsNullOrWhiteSpace(payload.Condition))
        {
            return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "Condition is required");
        }

        if (!CryptoHelper.TryGetEncryptionKey(out var encryptionKey, out var encryptionError))
        {
            return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, encryptionError ?? "AI key encryption not configured.");
        }

        var storedKey = await Data.GetAiKeyForParent(_cs, parentId);
        if (storedKey is null)
        {
            return await CreateErrorResponse(req, HttpStatusCode.NotFound, "AI key not configured for this account.");
        }

        string apiKey;
        try
        {
            apiKey = CryptoHelper.Decrypt(new EncryptedPayload(storedKey.CipherText, storedKey.Nonce, storedKey.Tag), encryptionKey);
        }
        catch (CryptographicException)
        {
            return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, "AI key could not be decrypted.");
        }

        var model = Environment.GetEnvironmentVariable("OPENAI_MODEL");
        if (string.IsNullOrWhiteSpace(model))
        {
            model = "gpt-4o-mini";
        }

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

        var userPrompt = $"Behavior category: {payload.DeedTypeName}\nNature: {(payload.IsPositive ? "positive" : "negative")}\nSpecific incident: {payload.Condition}\n\nSuggest appropriate screen time adjustment in minutes.";

        var requestBody = new
        {
            model,
            temperature = 0.5,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "v1/chat/completions")
        {
            Content = JsonContent.Create(requestBody)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var response = await HttpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            return await CreateErrorResponse(req, HttpStatusCode.BadGateway, "AI service unavailable.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        if (!document.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
        {
            return await CreateErrorResponse(req, HttpStatusCode.BadGateway, "AI response missing suggestions.");
        }

        var content = choices[0].GetProperty("message").GetProperty("content").GetString();
        if (string.IsNullOrWhiteSpace(content))
        {
            return await CreateErrorResponse(req, HttpStatusCode.BadGateway, "AI response empty.");
        }

        int? points;
        string? reason;
        try
        {
            using var suggestionJson = JsonDocument.Parse(content);
            points = suggestionJson.RootElement.TryGetProperty("points", out var pointsProp) ? pointsProp.GetInt32() : (int?)null;
            reason = suggestionJson.RootElement.TryGetProperty("reason", out var reasonProp) ? reasonProp.GetString() : null;
        }
        catch (JsonException)
        {
            return await CreateErrorResponse(req, HttpStatusCode.BadGateway, "AI response malformed.");
        }

        if (points is null || string.IsNullOrWhiteSpace(reason))
        {
            return await CreateErrorResponse(req, HttpStatusCode.BadGateway, "AI response malformed.");
        }

        var res = req.CreateResponse(HttpStatusCode.OK);
        await res.WriteAsJsonAsync(new AiSuggestResponse(points.Value, reason!));
        return res;
    }

    private static async Task<HttpResponseData> CreateErrorResponse(HttpRequestData req, HttpStatusCode status, string message)
    {
        var res = req.CreateResponse(status);
        await res.WriteAsJsonAsync(new { error = message });
        return res;
    }
}
