using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

public class AiKeysFunctions
{
    private readonly string _cs;

    public AiKeysFunctions(DbOptions options)
    {
        _cs = options?.ConnectionString ?? string.Empty;
        if (string.IsNullOrWhiteSpace(_cs))
        {
            throw new InvalidOperationException("Database connection string (DB environment variable) is not configured. Set the DB environment variable in Azure Static Web Apps configuration.");
        }
    }

    [Function("GetAiKeyStatus")]
    public async Task<HttpResponseData> GetAiKeyStatus(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "ai/key")] HttpRequestData req)
    {
        if (!ParentGuard.TryGetParent(req, _cs, out var parentId, out var guardError))
        {
            return guardError;
        }

        var existing = await Data.GetAiKeyForParent(_cs, parentId);
        var res = req.CreateResponse(HttpStatusCode.OK);
        await res.WriteAsJsonAsync(new AiKeyStatus(existing is not null));
        return res;
    }

    [Function("SetAiKey")]
    public async Task<HttpResponseData> SetAiKey(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "ai/key")] HttpRequestData req)
    {
        if (!ParentGuard.TryGetParent(req, _cs, out var parentId, out var guardError))
        {
            return guardError;
        }

        if (!CryptoHelper.TryGetEncryptionKey(out var encryptionKey, out var encryptionError))
        {
            return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, encryptionError ?? "AI key encryption not configured.");
        }

        AiKeyRequest? payload;
        try
        {
            payload = await req.ReadFromJsonAsync<AiKeyRequest>();
        }
        catch (JsonException)
        {
            return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "Invalid JSON payload");
        }

        if (payload is null || string.IsNullOrWhiteSpace(payload.ApiKey))
        {
            return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "API key is required");
        }

        var trimmed = payload.ApiKey.Trim();
        var encrypted = CryptoHelper.Encrypt(trimmed, encryptionKey);
        await Data.UpsertAiKeyForParent(_cs, parentId, new AiKeyRecord(encrypted.CipherText, encrypted.Nonce, encrypted.Tag));

        var res = req.CreateResponse(HttpStatusCode.OK);
        await res.WriteAsJsonAsync(new AiKeyStatus(true));
        return res;
    }

    [Function("DeleteAiKey")]
    public async Task<HttpResponseData> DeleteAiKey(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "ai/key")] HttpRequestData req)
    {
        if (!ParentGuard.TryGetParent(req, _cs, out var parentId, out var guardError))
        {
            return guardError;
        }

        await Data.DeleteAiKeyForParent(_cs, parentId);
        return req.CreateResponse(HttpStatusCode.NoContent);
    }

    private static async Task<HttpResponseData> CreateErrorResponse(HttpRequestData req, HttpStatusCode status, string message)
    {
        var res = req.CreateResponse(status);
        await res.WriteAsJsonAsync(new { error = message });
        return res;
    }
}
