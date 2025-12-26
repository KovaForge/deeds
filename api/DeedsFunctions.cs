using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

public class DeedsFunctions
{
    private readonly string _cs;

    public DeedsFunctions(DbOptions options)
    {
        _cs = options?.ConnectionString ?? string.Empty;
        if (string.IsNullOrWhiteSpace(_cs))
        {
            throw new InvalidOperationException("Database connection string (DB environment variable) is not configured. Set the DB environment variable in Azure Static Web Apps configuration.");
        }
    }

    [Function("CreateDeed")]
    public async Task<HttpResponseData> CreateDeed(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "deeds")] HttpRequestData req)
    {
        CreateDeed? payload;
        try
        {
            payload = await req.ReadFromJsonAsync<CreateDeed>();
        }
        catch (JsonException)
        {
            return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "Invalid JSON payload");
        }

        if (payload is null)
        {
            return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "Request body required");
        }

        if (payload.ChildId == Guid.Empty || payload.DeedTypeId == Guid.Empty)
        {
            return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "ChildId and DeedTypeId are required");
        }

        if (!ParentGuard.TryGetParent(req, _cs, payload.ParentId, out var parentId, out var parentError))
        {
            return parentError;
        }

        var child = await Data.GetChildById(_cs, payload.ChildId);
        if (child is null || child.ParentId != parentId)
        {
            return await CreateErrorResponse(req, HttpStatusCode.NotFound, "Child not found for this parent");
        }

        var deedType = await Data.GetDeedTypeById(_cs, payload.DeedTypeId);
        if (deedType is null || deedType.ParentId != parentId)
        {
            return await CreateErrorResponse(req, HttpStatusCode.NotFound, "Deed type not found for this parent");
        }

        if (!deedType.Active)
        {
            return await CreateErrorResponse(req, HttpStatusCode.Conflict, "Deed type is inactive");
        }

        var points = payload.Points ?? deedType.Points;
        if (points == 0)
        {
            return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "Points must resolve to a non-zero value");
        }

        var note = string.IsNullOrWhiteSpace(payload.Note) ? null : payload.Note.Trim();
        var occurredAt = DateTimeOffset.UtcNow;
        var createdBy = string.IsNullOrWhiteSpace(payload.CreatedBy) ? parentId.ToString() : payload.CreatedBy.Trim();

        var created = await Data.CreateDeed(_cs, payload.ChildId, payload.DeedTypeId, points, note, createdBy, occurredAt);
        var res = req.CreateResponse(HttpStatusCode.Created);
        await res.WriteAsJsonAsync(created);
        return res;
    }

    [Function("ListDeedsForChild")]
    public async Task<HttpResponseData> ListDeedsForChild(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "children/{childId:guid}/deeds")] HttpRequestData req,
        Guid childId)
    {
        if (!ParentGuard.TryGetParent(req, _cs, out var parentId, out var parentError))
        {
            return parentError;
        }

        var child = await Data.GetChildById(_cs, childId);
        if (child is null || child.ParentId != parentId)
        {
            return req.CreateResponse(HttpStatusCode.NotFound);
        }

        var deeds = await Data.GetDeedsForChild(_cs, childId);
        var res = req.CreateResponse(HttpStatusCode.OK);
        await res.WriteAsJsonAsync(deeds);
        return res;
    }

    [Function("DeleteDeed")]
    public async Task<HttpResponseData> DeleteDeed(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "children/{childId:guid}/deeds/{deedId:guid}")] HttpRequestData req,
        Guid childId,
        Guid deedId)
    {
        if (!ParentGuard.TryGetParent(req, _cs, out var parentId, out var parentError))
        {
            return parentError;
        }

        var child = await Data.GetChildById(_cs, childId);
        if (child is null)
        {
            return req.CreateResponse(HttpStatusCode.NotFound);
        }

        var details = await Data.GetDeedDetails(_cs, deedId);
        if (details is null || details.ChildId != childId || details.ParentId != parentId)
        {
            return req.CreateResponse(HttpStatusCode.NotFound);
        }

        var removed = await Data.DeleteDeed(_cs, deedId);
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
