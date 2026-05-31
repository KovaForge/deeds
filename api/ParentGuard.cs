using System.Linq;
using System.Text;
using System.Text.Json;
using System.Net;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

public static class ParentGuard
{
    private const string HeaderName = "x-parent-id";
    private const string ClientPrincipalHeader = "x-ms-client-principal";
    private const string AuthenticatedRole = "authenticated";
    private const string BearerPrefix = "Bearer ";

    public static bool TryGetParent(HttpRequestData req, string connectionString, out Guid parentId, out HttpResponseData? errorResponse)
        => TryGetParent(req, connectionString, null, out parentId, out errorResponse);

    public static bool TryGetParent(HttpRequestData req, string connectionString, Guid? payloadParentId, out Guid parentId, out HttpResponseData? errorResponse)
    {
        parentId = Guid.Empty;
        errorResponse = null;

        // --- Bearer token fallback ---
        if (TryGetParentFromBearer(req, connectionString, out var bearerParentId, out var bearerError))
        {
            parentId = bearerParentId;
            return true;
        }
        if (bearerError is not null)
        {
            errorResponse = bearerError;
            return false;
        }
        // --- End Bearer token ---

        var principal = GetPrincipal(req);
        if (principal.IsAuthenticated)
        {
            if (string.IsNullOrWhiteSpace(principal.Provider) || string.IsNullOrWhiteSpace(principal.UserId))
            {
                errorResponse = CreateError(req, HttpStatusCode.Forbidden, "Authenticated user is missing identity details.");
                return false;
            }

            var linkedParentId = Data.GetLinkedParentId(connectionString, principal.Provider, principal.UserId).GetAwaiter().GetResult();
            if (linkedParentId.HasValue)
            {
                parentId = linkedParentId.Value;
                if (payloadParentId.HasValue && payloadParentId.Value != Guid.Empty && payloadParentId.Value != parentId)
                {
                    errorResponse = CreateError(req, HttpStatusCode.Conflict, "ParentId does not match authenticated user.");
                    return false;
                }

                return true;
            }

            if (string.IsNullOrWhiteSpace(principal.Email))
            {
                errorResponse = CreateError(req, HttpStatusCode.Forbidden, "Authenticated user does not have an email claim.");
                return false;
            }

            var normalizedEmail = principal.Email.Trim().ToLowerInvariant();
            var parent = Data.GetParentByEmail(connectionString, normalizedEmail).GetAwaiter().GetResult();
            if (parent is null)
            {
                parent = Data.CreateParent(connectionString, normalizedEmail).GetAwaiter().GetResult();
            }

            parentId = parent.Id;
            Data.UpsertParentAuthLink(connectionString, principal.Provider, principal.UserId, parentId, normalizedEmail, null)
                .GetAwaiter().GetResult();
            if (payloadParentId.HasValue && payloadParentId.Value != Guid.Empty && payloadParentId.Value != parentId)
            {
                errorResponse = CreateError(req, HttpStatusCode.Conflict, "ParentId does not match authenticated user.");
                return false;
            }

            return true;
        }

        if (!AllowAnonymousParents())
        {
            errorResponse = CreateError(req, HttpStatusCode.Unauthorized, "Authentication required.");
            return false;
        }

        if (req.Headers.TryGetValues(HeaderName, out var headerValues))
        {
            var first = headerValues.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(first) && Guid.TryParse(first, out parentId))
            {
                if (payloadParentId.HasValue && payloadParentId.Value != Guid.Empty && payloadParentId.Value != parentId)
                {
                    errorResponse = CreateError(req, HttpStatusCode.Conflict, "ParentId in header does not match body");
                    return false;
                }

                return true;
            }
        }

        var query = QueryHelpers.ParseQuery(req.Url.Query);
        if (query.TryGetValue("parentId", out var parentValues) && Guid.TryParse(parentValues.ToString(), out parentId))
        {
            if (payloadParentId.HasValue && payloadParentId.Value != Guid.Empty && payloadParentId.Value != parentId)
            {
                errorResponse = CreateError(req, HttpStatusCode.Conflict, "ParentId in query does not match body");
                return false;
            }

            return true;
        }

        if (payloadParentId.HasValue && payloadParentId.Value != Guid.Empty)
        {
            parentId = payloadParentId.Value;
            return true;
        }

        errorResponse = CreateError(req, HttpStatusCode.BadRequest, "ParentId is required (header 'x-parent-id' or ?parentId=)");
        return false;
    }

    public static bool TryEnsureParent(HttpRequestData req, string connectionString, Guid expectedParent, out HttpResponseData? errorResponse)
    {
        errorResponse = null;
        var principal = GetPrincipal(req);
        if (principal.IsAuthenticated)
        {
            if (!TryGetParent(req, connectionString, expectedParent, out var parentId, out errorResponse))
            {
                return false;
            }

            if (parentId != expectedParent)
            {
                errorResponse = CreateError(req, HttpStatusCode.Forbidden, "ParentId does not match request target");
                return false;
            }

            return true;
        }

        if (!AllowAnonymousParents())
        {
            errorResponse = CreateError(req, HttpStatusCode.Unauthorized, "Authentication required.");
            return false;
        }

        return true;
    }

    public static bool TryGetAuthenticatedEmail(HttpRequestData req, out string? email, out HttpResponseData? errorResponse)
    {
        email = null;
        errorResponse = null;

        var principal = GetPrincipal(req);
        if (!principal.IsAuthenticated)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(principal.Email))
        {
            errorResponse = CreateError(req, HttpStatusCode.Forbidden, "Authenticated user does not have an email claim.");
            return false;
        }

        email = principal.Email;
        return true;
    }

    public static bool TryGetAuthenticatedUser(HttpRequestData req, out AuthUserInfo user, out HttpResponseData? errorResponse)
    {
        user = default;
        errorResponse = null;

        var principal = GetPrincipal(req);
        if (!principal.IsAuthenticated)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(principal.Provider) || string.IsNullOrWhiteSpace(principal.UserId))
        {
            errorResponse = CreateError(req, HttpStatusCode.Forbidden, "Authenticated user is missing identity details.");
            return false;
        }

        user = new AuthUserInfo(principal.Provider, principal.UserId, principal.Email);
        return true;
    }

    public static HttpResponseData CreateError(HttpRequestData req, HttpStatusCode status, string message)
    {
        var res = req.CreateResponse(status);
        res.WriteAsJsonAsync(new { error = message }).GetAwaiter().GetResult();
        return res;
    }

    public static bool AllowAnonymousParents()
    {
        var value = Environment.GetEnvironmentVariable("ALLOW_ANONYMOUS_PARENT");
        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    private static AuthPrincipal GetPrincipal(HttpRequestData req)
    {
        try
        {
            if (!req.Headers.TryGetValues(ClientPrincipalHeader, out var values))
            {
                return AuthPrincipal.Anonymous;
            }

            var encoded = values.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(encoded))
            {
                return AuthPrincipal.Anonymous;
            }

            var json = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            var roles = root.TryGetProperty("userRoles", out var rolesElement) && rolesElement.ValueKind == JsonValueKind.Array
                ? rolesElement.EnumerateArray().Select(x => x.GetString()).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray()
                : Array.Empty<string>();

            var isAuthenticated = roles.Any(role => string.Equals(role, AuthenticatedRole, StringComparison.OrdinalIgnoreCase));
            if (!isAuthenticated)
            {
                return AuthPrincipal.Anonymous;
            }

            var email = FindClaim(root, "preferred_username")
                ?? FindClaim(root, "email")
                ?? FindClaim(root, "upn");

            if (string.IsNullOrWhiteSpace(email))
            {
                var userDetails = root.TryGetProperty("userDetails", out var detailsElement) ? detailsElement.GetString() : null;
                if (IsEmail(userDetails))
                {
                    email = userDetails;
                }
            }

            var provider = root.TryGetProperty("identityProvider", out var providerElement) ? providerElement.GetString() : null;
            var userId = root.TryGetProperty("userId", out var userElement) ? userElement.GetString() : null;
            return new AuthPrincipal(true, provider, userId, email);
        }
        catch
        {
            return AuthPrincipal.Anonymous;
        }
    }

    private static string? FindClaim(JsonElement root, string type)
    {
        if (!root.TryGetProperty("claims", out var claimsElement) || claimsElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var claim in claimsElement.EnumerateArray())
        {
            if (!claim.TryGetProperty("typ", out var typElement) || !claim.TryGetProperty("val", out var valElement))
            {
                continue;
            }

            var claimType = typElement.GetString();
            if (string.Equals(claimType, type, StringComparison.OrdinalIgnoreCase))
            {
                return valElement.GetString();
            }
        }

        return null;
    }

    private static bool IsEmail(string? value)
        => !string.IsNullOrWhiteSpace(value) && value.Contains('@') && value.Contains('.');

    private static bool TryGetParentFromBearer(HttpRequestData req, string connectionString, out Guid parentId, out HttpResponseData? errorResponse)
    {
        parentId = Guid.Empty;
        errorResponse = null;

        if (!req.Headers.TryGetValues("Authorization", out var authHeaders))
        {
            return false;
        }

        var authHeader = authHeaders.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var rawToken = authHeader.Substring(BearerPrefix.Length).Trim();
        if (string.IsNullOrWhiteSpace(rawToken) || !rawToken.StartsWith("gd_pat_", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Convert raw token back to the internal hash format used forStorage
        // The raw token stored in the DB is the full gd_pat_xxx string
        // We hash it the same way the CLI does
        var tokenHash = CliTokenFunctions.HashToken(rawToken);
        var resolvedParentId = Data.ResolveCliToken(connectionString, tokenHash).GetAwaiter().GetResult();
        if (resolvedParentId.HasValue)
        {
            parentId = resolvedParentId.Value;
            return true;
        }

        return false;
    }

    private readonly record struct AuthPrincipal(bool IsAuthenticated, string? Provider, string? UserId, string? Email)
    {
        public static AuthPrincipal Anonymous => new(false, null, null, null);
    }

    public sealed record AuthUserInfo(string Provider, string UserId, string? Email);
}
