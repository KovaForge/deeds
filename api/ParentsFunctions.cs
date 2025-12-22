using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

public class ParentsFunctions
{
    private readonly string _cs;

    public ParentsFunctions(DbOptions options)
    {
        _cs = options.ConnectionString;
    }

    [Function("CreateParent")]
    public async Task<HttpResponseData> CreateParent(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "parents")] HttpRequestData req)
    {
        CreateParent? payload;
        try
        {
            payload = await req.ReadFromJsonAsync<CreateParent>();
        }
        catch (JsonException)
        {
            return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "Invalid JSON payload");
        }
        if (payload is null || string.IsNullOrWhiteSpace(payload.Email))
        {
            return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "Email is required");
        }

        var normalizedEmail = payload.Email.Trim().ToLowerInvariant();

        try
        {
            var existing = await Data.GetParentByEmail(_cs, normalizedEmail);
            if (existing is not null)
            {
                var existsResponse = req.CreateResponse(HttpStatusCode.OK);
                await existsResponse.WriteAsJsonAsync(existing);
                return existsResponse;
            }

            var created = await Data.CreateParent(_cs, normalizedEmail);
            var res = req.CreateResponse(HttpStatusCode.Created);
            await res.WriteAsJsonAsync(created);
            return res;
        }
        catch (Exception ex)
        {
            return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, $"Failed to create parent: {ex.Message}");
        }
    }

    [Function("GetParent")]
    public async Task<HttpResponseData> GetParent(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "parents/{id:guid}")] HttpRequestData req,
        Guid id)
    {
        var parent = await Data.GetParentById(_cs, id);
        if (parent is null)
        {
            return req.CreateResponse(HttpStatusCode.NotFound);
        }

        var res = req.CreateResponse(HttpStatusCode.OK);
        await res.WriteAsJsonAsync(parent);
        return res;
    }

    [Function("FindParentByEmail")]
    public async Task<HttpResponseData> FindParentByEmail(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "parents")] HttpRequestData req)
    {
    var query = QueryHelpers.ParseQuery(req.Url.Query);
    var email = query.TryGetValue("email", out var values) ? values.ToString().Trim() : null;
        if (string.IsNullOrWhiteSpace(email))
        {
            return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "Email query parameter is required");
        }

        try
        {
            var parent = await Data.GetParentByEmail(_cs, email.ToLowerInvariant());
            if (parent is null)
            {
                return req.CreateResponse(HttpStatusCode.NotFound);
            }

            var res = req.CreateResponse(HttpStatusCode.OK);
            await res.WriteAsJsonAsync(parent);
            return res;
        }
        catch (Exception ex)
        {
            return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, $"Failed to find parent: {ex.Message}");
        }
    }

    private static async Task<HttpResponseData> CreateErrorResponse(HttpRequestData req, HttpStatusCode status, string message)
    {
        var res = req.CreateResponse(status);
        await res.WriteAsJsonAsync(new { error = message });
        return res;
    }
}
