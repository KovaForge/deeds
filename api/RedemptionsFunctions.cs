using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

public class RedemptionsFunctions
{
    private readonly string _cs;

    public RedemptionsFunctions(DbOptions options)
    {
        _cs = options?.ConnectionString ?? string.Empty;
        if (string.IsNullOrWhiteSpace(_cs))
        {
            throw new InvalidOperationException("Database connection string (DB environment variable) is not configured. Set the DB environment variable in Azure Static Web Apps configuration.");
        }
    }

    [Function("CreateRedemption")]
    public async Task<HttpResponseData> CreateRedemption(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "redemptions")] HttpRequestData req)
    {
        CreateRedemption? payload;
        try
        {
            payload = await req.ReadFromJsonAsync<CreateRedemption>();
        }
        catch (JsonException)
        {
            return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "Invalid JSON payload");
        }

        if (payload is null)
        {
            return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "Request body required");
        }

        if (payload.ChildId == Guid.Empty)
        {
            return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "ChildId is required");
        }

        if (payload.Points <= 0)
        {
            return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "Points must be greater than zero");
        }

        if (!ParentGuard.TryGetParent(req, payload.ParentId, out var parentId, out var parentError))
        {
            return parentError;
        }

        var child = await Data.GetChildById(_cs, payload.ChildId);
        if (child is null || child.ParentId != parentId)
        {
            return req.CreateResponse(HttpStatusCode.NotFound);
        }

        var balance = await Data.GetBalance(_cs, payload.ChildId);
        if (balance is null)
        {
            return req.CreateResponse(HttpStatusCode.NotFound);
        }

        if (balance.Points < payload.Points)
        {
            return await CreateErrorResponse(req, HttpStatusCode.Conflict, "Insufficient points to redeem");
        }

        var description = string.IsNullOrWhiteSpace(payload.Description) ? null : payload.Description.Trim();
        var createdBy = string.IsNullOrWhiteSpace(payload.CreatedBy) ? parentId.ToString() : payload.CreatedBy.Trim();
        var created = await Data.CreateRedemption(_cs, payload.ChildId, payload.Points, description, createdBy, DateTimeOffset.UtcNow);
        var res = req.CreateResponse(HttpStatusCode.Created);
        await res.WriteAsJsonAsync(created);
        return res;
    }

    [Function("ListRedemptionsForChild")]
    public async Task<HttpResponseData> ListRedemptionsForChild(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "children/{childId:guid}/redemptions")] HttpRequestData req,
        Guid childId)
    {
        if (!ParentGuard.TryGetParent(req, out var parentId, out var parentError))
        {
            return parentError;
        }

        var child = await Data.GetChildById(_cs, childId);
        if (child is null || child.ParentId != parentId)
        {
            return req.CreateResponse(HttpStatusCode.NotFound);
        }

        var redemptions = await Data.GetRedemptionsForChild(_cs, childId);
        var res = req.CreateResponse(HttpStatusCode.OK);
        await res.WriteAsJsonAsync(redemptions);
        return res;
    }

    private static async Task<HttpResponseData> CreateErrorResponse(HttpRequestData req, HttpStatusCode status, string message)
    {
        var res = req.CreateResponse(status);
        await res.WriteAsJsonAsync(new { error = message });
        return res;
    }
}
