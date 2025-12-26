using System.Linq;
using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

public class ParentInvitesFunctions
{
    private readonly string _cs;

    public ParentInvitesFunctions(DbOptions options)
    {
        _cs = options?.ConnectionString ?? string.Empty;
        if (string.IsNullOrWhiteSpace(_cs))
        {
            throw new InvalidOperationException("Database connection string (DB environment variable) is not configured. Set the DB environment variable in Azure Static Web Apps configuration.");
        }
    }

    [Function("CreateParentInvite")]
    public async Task<HttpResponseData> CreateParentInvite(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "parents/{parentId:guid}/invites")] HttpRequestData req,
        Guid parentId)
    {
        if (!ParentGuard.TryEnsureParent(req, _cs, parentId, out var guardError))
        {
            return guardError!;
        }

        if (!ParentGuard.TryGetAuthenticatedUser(req, out var user, out var authError))
        {
            return authError ?? ParentGuard.CreateError(req, HttpStatusCode.Unauthorized, "Authentication required.");
        }

        CreateInvite? payload;
        try
        {
            payload = await req.ReadFromJsonAsync<CreateInvite>();
        }
        catch (JsonException)
        {
            return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "Invalid JSON payload");
        }

        if (payload is null || string.IsNullOrWhiteSpace(payload.Email))
        {
            return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "Email is required");
        }

        if (payload.ParentId != Guid.Empty && payload.ParentId != parentId)
        {
            return await CreateErrorResponse(req, HttpStatusCode.Conflict, "ParentId in body does not match route.");
        }

        var normalizedEmail = payload.Email.Trim().ToLowerInvariant();
        var days = payload.DaysValid.GetValueOrDefault(7);
        if (days <= 0) days = 7;
        var expires = DateTime.UtcNow.AddDays(days);

        try
        {
            var created = await Data.CreateParentInvite(_cs, parentId, normalizedEmail, expires, user.Email ?? user.UserId);
            var res = req.CreateResponse(HttpStatusCode.Created);
            await res.WriteAsJsonAsync(new InviteResponse(created.Id, created.Token, created.ExpiresAtUtc));
            return res;
        }
        catch (Exception ex)
        {
            return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, $"Failed to create invite: {ex.Message}");
        }
    }

    [Function("ListParentInvites")]
    public async Task<HttpResponseData> ListParentInvites(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "parents/{parentId:guid}/invites")] HttpRequestData req,
        Guid parentId)
    {
        if (!ParentGuard.TryEnsureParent(req, _cs, parentId, out var guardError))
        {
            return guardError!;
        }

        if (!ParentGuard.TryGetAuthenticatedUser(req, out _, out var authError))
        {
            return authError ?? ParentGuard.CreateError(req, HttpStatusCode.Unauthorized, "Authentication required.");
        }

        var invites = await Data.ListPendingParentInvites(_cs, parentId);
        var res = req.CreateResponse(HttpStatusCode.OK);
        await res.WriteAsJsonAsync(invites.Select(i => new ParentInviteDto(i.Id, i.ParentId, i.Email, i.ExpiresAtUtc, i.CreatedAtUtc, i.CreatedBy)));
        return res;
    }

    [Function("CancelParentInvite")]
    public async Task<HttpResponseData> CancelParentInvite(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "parents/{parentId:guid}/invites/{inviteId:guid}")] HttpRequestData req,
        Guid parentId,
        Guid inviteId)
    {
        if (!ParentGuard.TryEnsureParent(req, _cs, parentId, out var guardError))
        {
            return guardError!;
        }

        if (!ParentGuard.TryGetAuthenticatedUser(req, out _, out var authError))
        {
            return authError ?? ParentGuard.CreateError(req, HttpStatusCode.Unauthorized, "Authentication required.");
        }

        var cancelled = await Data.CancelParentInvite(_cs, parentId, inviteId);
        if (!cancelled)
        {
            return ParentGuard.CreateError(req, HttpStatusCode.NotFound, "Invite not found or already handled.");
        }

        var res = req.CreateResponse(HttpStatusCode.OK);
        await res.WriteAsJsonAsync(new { cancelled = inviteId });
        return res;
    }

    [Function("AcceptParentInvite")]
    public async Task<HttpResponseData> AcceptParentInvite(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "invites/accept")] HttpRequestData req)
    {
        if (!ParentGuard.TryGetAuthenticatedUser(req, out var user, out var authError))
        {
            return authError ?? ParentGuard.CreateError(req, HttpStatusCode.Unauthorized, "Authentication required.");
        }

        AcceptInviteRequest? payload;
        try
        {
            payload = await req.ReadFromJsonAsync<AcceptInviteRequest>();
        }
        catch (JsonException)
        {
            return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "Invalid JSON payload");
        }

        if (payload is null || string.IsNullOrWhiteSpace(payload.Token))
        {
            return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "Token is required");
        }

        var accepted = await Data.AcceptParentInvite(_cs, payload.Token.Trim(), $"{user.Provider}:{user.UserId}");
        if (!accepted.HasValue)
        {
            return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "Invite is invalid or expired.");
        }

        var (parentId, email) = accepted.Value;
        await Data.UpsertParentAuthLink(_cs, user.Provider, user.UserId, parentId, user.Email ?? email, null);

        var res = req.CreateResponse(HttpStatusCode.OK);
        await res.WriteAsJsonAsync(new AcceptInviteResponse(parentId, email));
        return res;
    }

    private static async Task<HttpResponseData> CreateErrorResponse(HttpRequestData req, HttpStatusCode status, string message)
    {
        var res = req.CreateResponse(status);
        await res.WriteAsJsonAsync(new { error = message });
        return res;
    }
}
