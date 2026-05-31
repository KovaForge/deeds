using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

public class CliTokenFunctions
{
    private readonly string _cs;

    public CliTokenFunctions(DbOptions options)
    {
        _cs = options?.ConnectionString ?? string.Empty;
    }

    [Function("CreateCliToken")]
    public async Task<HttpResponseData> CreateCliToken(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "cli-tokens")] HttpRequestData req)
    {
        if (!ParentGuard.TryGetParent(req, _cs, out var parentId, out var authError))
        {
            return authError!;
        }

        CreateCliTokenRequest? payload;
        try
        {
            payload = await req.ReadFromJsonAsync<CreateCliTokenRequest>();
        }
        catch
        {
            return Error(req, HttpStatusCode.BadRequest, "Invalid JSON payload");
        }

        if (payload is null || string.IsNullOrWhiteSpace(payload.Label))
        {
            return Error(req, HttpStatusCode.BadRequest, "Label is required");
        }

        var rawToken = $"gd_pat_{Base64UrlEncode(RandomNumberGenerator.GetBytes(24))}";
        var tokenHash = HashToken(rawToken);
        DateTime? expiresAt = null;
        if (payload.DaysValid.HasValue && payload.DaysValid.Value > 0)
        {
            expiresAt = DateTime.UtcNow.AddDays(payload.DaysValid.Value);
        }

        try
        {
            var created = await Data.CreateCliToken(_cs, parentId, payload.Label, tokenHash, expiresAt);
            var res = req.CreateResponse(HttpStatusCode.Created);
            await res.WriteAsJsonAsync(new CliTokenCreatedResponse(
                Id: created.Id,
                Token: rawToken,
                Label: created.Label,
                CreatedAt: created.CreatedAt));
            return res;
        }
        catch (Exception ex)
        {
            return Error(req, HttpStatusCode.InternalServerError, $"Failed to create token: {ex.Message}");
        }
    }

    [Function("ListCliTokens")]
    public async Task<HttpResponseData> ListCliTokens(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "cli-tokens")] HttpRequestData req)
    {
        if (!ParentGuard.TryGetParent(req, _cs, out var parentId, out var authError))
        {
            return authError!;
        }

        try
        {
            var tokens = await Data.ListCliTokens(_cs, parentId);
            var res = req.CreateResponse(HttpStatusCode.OK);
            await res.WriteAsJsonAsync(tokens.Select(t => new CliTokenInfoResponse(
                Id: t.Id,
                Label: t.Label,
                CreatedAt: t.CreatedAt,
                LastUsedAt: t.LastUsedAt,
                ExpiresAt: t.ExpiresAt)));
            return res;
        }
        catch (Exception ex)
        {
            return Error(req, HttpStatusCode.InternalServerError, $"Failed to list tokens: {ex.Message}");
        }
    }

    [Function("RevokeCliToken")]
    public async Task<HttpResponseData> RevokeCliToken(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "cli-tokens/{id:guid}")] HttpRequestData req,
        Guid id)
    {
        if (!ParentGuard.TryGetParent(req, _cs, out var parentId, out var authError))
        {
            return authError!;
        }

        try
        {
            var revoked = await Data.RevokeCliToken(_cs, parentId, id);
            if (!revoked)
            {
                return Error(req, HttpStatusCode.NotFound, "Token not found or already revoked");
            }

            var res = req.CreateResponse(HttpStatusCode.OK);
            await res.WriteAsJsonAsync(new { success = true });
            return res;
        }
        catch (Exception ex)
        {
            return Error(req, HttpStatusCode.InternalServerError, $"Failed to revoke token: {ex.Message}");
        }
    }

    public static string HashToken(string token)
    {
        var bytes = Encoding.UTF8.GetBytes(token);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private static HttpResponseData Error(HttpRequestData req, HttpStatusCode status, string message)
    {
        var res = req.CreateResponse(status);
        res.WriteAsJsonAsync(new { error = message }).GetAwaiter().GetResult();
        return res;
    }
}

public record CreateCliTokenRequest(string Label, int? DaysValid);
public record CliTokenCreatedResponse(Guid Id, string Token, string Label, DateTime CreatedAt);
public record CliTokenInfoResponse(Guid Id, string Label, DateTime CreatedAt, DateTime? LastUsedAt, DateTime? ExpiresAt);
