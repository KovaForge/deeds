using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

public class ProfileFunctions
{
    private readonly string _cs;

    public ProfileFunctions(DbOptions options)
    {
        _cs = options?.ConnectionString ?? string.Empty;
        if (string.IsNullOrWhiteSpace(_cs))
        {
            throw new InvalidOperationException("Database connection string (DB environment variable) is not configured. Set the DB environment variable in Azure Static Web Apps configuration.");
        }
    }

    [Function("Profile_Get")]
    public async Task<HttpResponseData> GetProfileAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "profile")] HttpRequestData req)
    {
        if (!ParentGuard.TryGetAuthenticatedUser(req, out var user, out var authError))
        {
            return authError ?? ParentGuard.CreateError(req, HttpStatusCode.Unauthorized, "Authentication required.");
        }

        if (!ParentGuard.TryGetParent(req, _cs, out var parentId, out var parentError))
        {
            return parentError!;
        }

        var profile = await Data.GetProfile(_cs, user.Provider, user.UserId)
                      ?? new ProfileDto(user.Email, null);

        var res = req.CreateResponse(HttpStatusCode.OK);
        await res.WriteAsJsonAsync(profile);
        return res;
    }

    [Function("Profile_Update")]
    public async Task<HttpResponseData> UpdateProfileAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "profile")] HttpRequestData req)
    {
        if (!ParentGuard.TryGetAuthenticatedUser(req, out var user, out var authError))
        {
            return authError ?? ParentGuard.CreateError(req, HttpStatusCode.Unauthorized, "Authentication required.");
        }

        if (!ParentGuard.TryGetParent(req, _cs, out var parentId, out var parentError))
        {
            return parentError!;
        }

        UpdateProfile? payload;
        try
        {
            payload = await req.ReadFromJsonAsync<UpdateProfile>();
        }
        catch (JsonException)
        {
            return ParentGuard.CreateError(req, HttpStatusCode.BadRequest, "Invalid JSON payload.");
        }

        if (payload is null || string.IsNullOrWhiteSpace(payload.DisplayName))
        {
            return ParentGuard.CreateError(req, HttpStatusCode.BadRequest, "Display name is required.");
        }

        var trimmed = payload.DisplayName.Trim();
        await Data.UpsertParentAuthLink(_cs, user.Provider, user.UserId, parentId, user.Email, trimmed);

        var res = req.CreateResponse(HttpStatusCode.OK);
        await res.WriteAsJsonAsync(new ProfileDto(user.Email, trimmed));
        return res;
    }
}
