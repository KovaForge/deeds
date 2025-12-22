using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

public class ChildrenFunctions
{
    private readonly string _cs;

    public ChildrenFunctions(DbOptions options)
    {
        _cs = options.ConnectionString;
    }

    [Function("CreateChild")]
    public async Task<HttpResponseData> CreateChild(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "children")] HttpRequestData req)
    {
        CreateChild? payload;

        try
        {
            payload = await req.ReadFromJsonAsync<CreateChild>();
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
        if (!ParentGuard.TryGetParent(req, payloadParentId, out var parentId, out var guardError))
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
            return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "Child name is required");
        }

        var name = payload.Name.Trim();
        var rate = payload.DollarPerPoint ?? 1.0m;
        if (rate <= 0)
        {
            return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "DollarPerPoint must be greater than zero");
        }

        try
        {
            var created = await Data.CreateChild(_cs, parentId, name, rate);
            var res = req.CreateResponse(HttpStatusCode.Created);
            await res.WriteAsJsonAsync(created);
            return res;
        }
        catch (Exception ex)
        {
            return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, $"Failed to create child: {ex.Message}");
        }
    }

    [Function("ListChildren")]
    public async Task<HttpResponseData> ListChildren(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "parents/{parentId:guid}/children")] HttpRequestData req,
        Guid parentId)
    {
        if (!ParentGuard.TryEnsureParent(req, parentId, out var guardError))
        {
            return guardError!;
        }

        var parent = await Data.GetParentById(_cs, parentId);
        if (parent is null)
        {
            return req.CreateResponse(HttpStatusCode.NotFound);
        }

        var children = await Data.GetChildrenForParent(_cs, parentId);
        var res = req.CreateResponse(HttpStatusCode.OK);
        await res.WriteAsJsonAsync(children);
        return res;
    }

    [Function("UpdateChild")]
    public async Task<HttpResponseData> UpdateChild(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", "patch", Route = "children/{childId:guid}")] HttpRequestData req,
        Guid childId)
    {
        UpdateChild? payload;
        try
        {
            payload = await req.ReadFromJsonAsync<UpdateChild>();
        }
        catch (JsonException)
        {
            return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "Invalid JSON payload");
        }

        if (payload is null)
        {
            return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "Request body required");
        }

        if (!ParentGuard.TryGetParent(req, payload.ParentId, out var parentId, out var error))
        {
            return error;
        }

        var existing = await Data.GetChildById(_cs, childId);
        if (existing is null)
        {
            return req.CreateResponse(HttpStatusCode.NotFound);
        }

        if (existing.ParentId != parentId)
        {
            return req.CreateResponse(HttpStatusCode.NotFound);
        }

        if (payload.ParentId != Guid.Empty && payload.ParentId != existing.ParentId)
        {
            return await CreateErrorResponse(req, HttpStatusCode.Conflict, "ParentId mismatch");
        }

        if (string.IsNullOrWhiteSpace(payload.Name))
        {
            return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "Child name is required");
        }

        var name = payload.Name.Trim();
        var rate = payload.DollarPerPoint ?? existing.DollarPerPoint;
        if (rate <= 0)
        {
            return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "DollarPerPoint must be greater than zero");
        }

        var updated = await Data.UpdateChild(_cs, childId, name, rate);
        if (updated is null)
        {
            return req.CreateResponse(HttpStatusCode.NotFound);
        }

        var res = req.CreateResponse(HttpStatusCode.OK);
        await res.WriteAsJsonAsync(updated);
        return res;
    }

    [Function("GetChild")]
    public async Task<HttpResponseData> GetChild(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "children/{childId:guid}")] HttpRequestData req,
        Guid childId)
    {
        if (!ParentGuard.TryGetParent(req, out var parentId, out var error))
        {
            return error;
        }

        var child = await Data.GetChildById(_cs, childId);
        if (child is null || child.ParentId != parentId)
        {
            return req.CreateResponse(HttpStatusCode.NotFound);
        }

        var res = req.CreateResponse(HttpStatusCode.OK);
        await res.WriteAsJsonAsync(child);
        return res;
    }

    [Function("DeleteChild")]
    public async Task<HttpResponseData> DeleteChild(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "parents/{parentId:guid}/children/{childId:guid}")] HttpRequestData req,
        Guid parentId,
        Guid childId)
    {
        if (!ParentGuard.TryEnsureParent(req, parentId, out var guardError))
        {
            return guardError!;
        }

        var child = await Data.GetChildById(_cs, childId);
        if (child is null || child.ParentId != parentId)
        {
            return req.CreateResponse(HttpStatusCode.NotFound);
        }

        var removed = await Data.DeleteChild(_cs, childId);
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
