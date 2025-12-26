using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

public class DeedTypesFunctions
{
    private readonly string _cs;

    public DeedTypesFunctions(DbOptions options)
    {
        _cs = options?.ConnectionString ?? string.Empty;
        if (string.IsNullOrWhiteSpace(_cs))
        {
            throw new InvalidOperationException("Database connection string (DB environment variable) is not configured. Set the DB environment variable in Azure Static Web Apps configuration.");
        }
    }

    [Function("CreateDeedType")]
    public async Task<HttpResponseData> CreateDeedType(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "deed-types")] HttpRequestData req)
    {
        CreateDeedType? payload;
        try
        {
            payload = await req.ReadFromJsonAsync<CreateDeedType>();
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

        if (payload.Points == 0)
        {
            return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "Points must be non-zero to indicate good or bad deed");
        }

        var normalizedName = payload.Name.Trim();
        var existing = await Data.GetDeedTypeByName(_cs, parentId, normalizedName);
        if (existing is not null)
        {
            return await CreateErrorResponse(req, HttpStatusCode.Conflict, "Deed type already exists");
        }

        var created = await Data.CreateDeedType(_cs, parentId, normalizedName, payload.Points);
        var res = req.CreateResponse(HttpStatusCode.Created);
        await res.WriteAsJsonAsync(created);
        return res;
    }

    [Function("ListDeedTypes")]
    public async Task<HttpResponseData> ListDeedTypes(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "parents/{parentId:guid}/deed-types")] HttpRequestData req,
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

        var items = await Data.GetDeedTypesForParent(_cs, parentId);
        var res = req.CreateResponse(HttpStatusCode.OK);
        await res.WriteAsJsonAsync(items);
        return res;
    }

    [Function("UpdateDeedType")]
    public async Task<HttpResponseData> UpdateDeedType(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", "patch", Route = "deed-types/{deedTypeId:guid}")] HttpRequestData req,
        Guid deedTypeId)
    {
        UpdateDeedType? payload;
        try
        {
            payload = await req.ReadFromJsonAsync<UpdateDeedType>();
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

        var existing = await Data.GetDeedTypeById(_cs, deedTypeId);
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

        if (payload.Points == 0)
        {
            return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "Points must be non-zero");
        }

        var normalizedName = payload.Name.Trim();
        var conflicting = await Data.GetDeedTypeByName(_cs, existing.ParentId, normalizedName);
        if (conflicting is not null && conflicting.Id != deedTypeId)
        {
            return await CreateErrorResponse(req, HttpStatusCode.Conflict, "Another deed type with that name exists");
        }

        var updated = await Data.UpdateDeedType(_cs, deedTypeId, normalizedName, payload.Points, payload.Active);
        if (updated is null)
        {
            return req.CreateResponse(HttpStatusCode.NotFound);
        }

        var res = req.CreateResponse(HttpStatusCode.OK);
        await res.WriteAsJsonAsync(updated);
        return res;
    }

    [Function("DeleteDeedType")]
    public async Task<HttpResponseData> DeleteDeedType(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "parents/{parentId:guid}/deed-types/{deedTypeId:guid}")] HttpRequestData req,
        Guid parentId,
        Guid deedTypeId)
    {
        if (!ParentGuard.TryEnsureParent(req, _cs, parentId, out var guardError))
        {
            return guardError!;
        }

        var details = await Data.GetDeedTypeDetails(_cs, deedTypeId);
        if (details is null)
        {
            return req.CreateResponse(HttpStatusCode.NotFound);
        }

        if (details.ParentId != parentId)
        {
            return req.CreateResponse(HttpStatusCode.NotFound);
        }

        var deedUsage = await Data.CountDeedsUsingDeedType(_cs, deedTypeId);
        if (deedUsage > 0)
        {
            return await CreateErrorResponse(req, HttpStatusCode.Conflict, "Cannot delete this deed type while deeds still reference it. Delete those deeds first.");
        }

        var removed = await Data.DeleteDeedType(_cs, deedTypeId);
        if (!removed)
        {
            return req.CreateResponse(HttpStatusCode.NotFound);
        }

        return req.CreateResponse(HttpStatusCode.NoContent);
    }

    private static async Task<HttpResponseData> CreateErrorResponse(HttpRequestData req, HttpStatusCode status, string message)
    {
        var res = req.CreateResponse(status);
        await res.WriteAsJsonAsync(new { error = message });
        return res;
    }
}
