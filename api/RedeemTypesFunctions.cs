using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

public class RedeemTypesFunctions
{
    private readonly string _cs;

    public RedeemTypesFunctions(DbOptions options)
    {
        _cs = options?.ConnectionString ?? string.Empty;
        if (string.IsNullOrWhiteSpace(_cs))
        {
            throw new InvalidOperationException("Database connection string (DB environment variable) is not configured. Set the DB environment variable in Azure Static Web Apps configuration.");
        }
        Data.EnsureSchema(_cs).GetAwaiter().GetResult();
    }

    [Function("CreateRedeemType")]
    public async Task<HttpResponseData> CreateRedeemType(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "redeem-types")] HttpRequestData req)
    {
        CreateRedeemType? payload;
        try
        {
            payload = await req.ReadFromJsonAsync<CreateRedeemType>();
        }
        catch (JsonException)
        {
            return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "Invalid JSON payload");
        }

        if (payload is null)
        {
            return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "Request body required");
        }

        var payloadParentId = payload.ParentId;
        if (!ParentGuard.TryGetParent(req, _cs, payloadParentId, out var parentId, out var guardError))
        {
            return guardError!;
        }

        var parent = await Data.GetParentById(_cs, parentId);
        if (parent is null)
        {
            return req.CreateResponse(HttpStatusCode.NotFound);
        }

        if (string.IsNullOrWhiteSpace(payload.Name))
        {
            return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "Name is required");
        }

        if (payload.Points <= 0)
        {
            return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "Points must be greater than zero");
        }

        var normalizedName = payload.Name.Trim();
        var existing = await Data.GetRedeemTypeByName(_cs, parentId, normalizedName);
        if (existing is not null)
        {
            return await CreateErrorResponse(req, HttpStatusCode.Conflict, "Redeem type already exists");
        }

        try
        {
            var created = await Data.CreateRedeemType(_cs, parentId, normalizedName, payload.Points);
            var res = req.CreateResponse(HttpStatusCode.Created);
            await res.WriteAsJsonAsync(created);
            return res;
        }
        catch (Exception ex)
        {
            return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, $"Failed to create redeem type: {ex.Message}");
        }
    }

    [Function("ListRedeemTypes")]
    public async Task<HttpResponseData> ListRedeemTypes(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "parents/{parentId:guid}/redeem-types")] HttpRequestData req,
        Guid parentId)
    {
        if (!ParentGuard.TryEnsureParent(req, _cs, parentId, out var guardError))
        {
            return guardError!;
        }

        var parent = await Data.GetParentById(_cs, parentId);
        if (parent is null)
        {
            return req.CreateResponse(HttpStatusCode.NotFound);
        }

        try
        {
            var items = await Data.GetRedeemTypesForParent(_cs, parentId);
            var res = req.CreateResponse(HttpStatusCode.OK);
            await res.WriteAsJsonAsync(items);
            return res;
        }
        catch (Exception ex)
        {
            return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, $"Failed to list redeem types: {ex.Message}");
        }
    }

    [Function("UpdateRedeemType")]
    public async Task<HttpResponseData> UpdateRedeemType(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", "patch", Route = "redeem-types/{redeemTypeId:guid}")] HttpRequestData req,
        Guid redeemTypeId)
    {
        UpdateRedeemType? payload;
        try
        {
            payload = await req.ReadFromJsonAsync<UpdateRedeemType>();
        }
        catch (JsonException)
        {
            return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "Invalid JSON payload");
        }

        if (payload is null)
        {
            return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "Request body required");
        }

        if (!ParentGuard.TryGetParent(req, _cs, payload.ParentId, out var parentId, out var parentError))
        {
            return parentError;
        }

        var existing = await Data.GetRedeemTypeById(_cs, redeemTypeId);
        if (existing is null)
        {
            return req.CreateResponse(HttpStatusCode.NotFound);
        }

        if (payload.ParentId != Guid.Empty && payload.ParentId != existing.ParentId)
        {
            return await CreateErrorResponse(req, HttpStatusCode.Conflict, "ParentId mismatch");
        }

        if (existing.ParentId != parentId)
        {
            return req.CreateResponse(HttpStatusCode.NotFound);
        }

        if (string.IsNullOrWhiteSpace(payload.Name))
        {
            return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "Name is required");
        }

        if (payload.Points <= 0)
        {
            return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "Points must be greater than zero");
        }

        var normalizedName = payload.Name.Trim();
        var conflicting = await Data.GetRedeemTypeByName(_cs, existing.ParentId, normalizedName);
        if (conflicting is not null && conflicting.Id != redeemTypeId)
        {
            return await CreateErrorResponse(req, HttpStatusCode.Conflict, "Another redeem type with that name exists");
        }

        try
        {
            var updated = await Data.UpdateRedeemType(_cs, redeemTypeId, normalizedName, payload.Points, payload.Active);
            if (updated is null)
            {
                return req.CreateResponse(HttpStatusCode.NotFound);
            }

            var res = req.CreateResponse(HttpStatusCode.OK);
            await res.WriteAsJsonAsync(updated);
            return res;
        }
        catch (Exception ex)
        {
            return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, $"Failed to update redeem type: {ex.Message}");
        }
    }

    [Function("DeleteRedeemType")]
    public async Task<HttpResponseData> DeleteRedeemType(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "parents/{parentId:guid}/redeem-types/{redeemTypeId:guid}")] HttpRequestData req,
        Guid parentId,
        Guid redeemTypeId)
    {
        if (!ParentGuard.TryEnsureParent(req, _cs, parentId, out var guardError))
        {
            return guardError!;
        }

        var details = await Data.GetRedeemTypeById(_cs, redeemTypeId);
        if (details is null)
        {
            return req.CreateResponse(HttpStatusCode.NotFound);
        }

        if (details.ParentId != parentId)
        {
            return req.CreateResponse(HttpStatusCode.NotFound);
        }

        try
        {
            var removed = await Data.DeleteRedeemType(_cs, redeemTypeId);
            if (!removed)
            {
                return req.CreateResponse(HttpStatusCode.NotFound);
            }

            return req.CreateResponse(HttpStatusCode.NoContent);
        }
        catch (Exception ex)
        {
            return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, $"Failed to delete redeem type: {ex.Message}");
        }
    }

    private static async Task<HttpResponseData> CreateErrorResponse(HttpRequestData req, HttpStatusCode status, string message)
    {
        var res = req.CreateResponse(status);
        await res.WriteAsJsonAsync(new { error = message });
        return res;
    }
}
