using System.Net.Http.Json;
using System.Linq;

namespace GoodDeeds.Client.Services;

public sealed class AuthService
{
    private readonly HttpClient _http;
    private AuthUser? _cached;
    private bool _loaded;

    public AuthService(HttpClient http)
    {
        _http = http;
    }

    public async Task<AuthUser?> GetUserAsync()
    {
        if (_loaded)
        {
            return _cached;
        }

        _loaded = true;

        try
        {
            var response = await _http.GetFromJsonAsync<AuthMeResponse>("/.auth/me");
            var principal = response?.ClientPrincipal;
            if (principal is null || principal.UserRoles is null || !principal.UserRoles.Contains("authenticated"))
            {
                return null;
            }

            var email = ResolveEmail(principal);
            var displayName = ResolveDisplayName(principal, email);
            _cached = new AuthUser(principal.IdentityProvider, principal.UserId, principal.UserDetails, email, displayName);
            return _cached;
        }
        catch
        {
            return null;
        }
    }

    public void ClearCache()
    {
        _loaded = false;
        _cached = null;
    }

    private static string? ResolveEmail(ClientPrincipal principal)
    {
        var claim = FindClaim(principal, "preferred_username")
            ?? FindClaim(principal, "email")
            ?? FindClaim(principal, "upn");

        if (IsEmail(claim))
        {
            return claim;
        }

        return IsEmail(principal.UserDetails) ? principal.UserDetails : null;
    }

    private static string? ResolveDisplayName(ClientPrincipal principal, string? email)
    {
        var claim = FindClaim(principal, "name") ?? FindClaim(principal, "given_name");
        if (!string.IsNullOrWhiteSpace(claim))
        {
            return claim;
        }

        return email ?? principal.UserDetails;
    }

    private static string? FindClaim(ClientPrincipal principal, string type)
        => principal.Claims?.FirstOrDefault(c => string.Equals(c.Typ, type, StringComparison.OrdinalIgnoreCase))?.Val;

    private static bool IsEmail(string? value)
        => !string.IsNullOrWhiteSpace(value) && value.Contains('@') && value.Contains('.');
}
