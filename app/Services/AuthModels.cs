using System.Text.Json.Serialization;

namespace GoodDeeds.Client.Services;

public sealed record AuthMeResponse(
    [property: JsonPropertyName("clientPrincipal")] ClientPrincipal? ClientPrincipal);

public sealed record ClientPrincipal(
    [property: JsonPropertyName("identityProvider")] string IdentityProvider,
    [property: JsonPropertyName("userId")] string UserId,
    [property: JsonPropertyName("userDetails")] string UserDetails,
    [property: JsonPropertyName("userRoles")] string[] UserRoles,
    [property: JsonPropertyName("claims")] ClientPrincipalClaim[] Claims);

public sealed record ClientPrincipalClaim(
    [property: JsonPropertyName("typ")] string Typ,
    [property: JsonPropertyName("val")] string Val);

public sealed record AuthUser(
    string Provider,
    string UserId,
    string UserDetails,
    string? Email,
    string? DisplayName);
