using System.Net.Http.Json;
using GoodDeeds.Client.Models;

namespace GoodDeeds.Client.Services;

public class ApiClient
{
    private readonly HttpClient _http;

    public ApiClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<ParentDto> CreateParentAsync(string email)
    {
        var response = await _http.PostAsJsonAsync("parents", new CreateParentRequest(email));
        if (response.IsSuccessStatusCode)
        {
            return (await response.Content.ReadFromJsonAsync<ParentDto>())!;
        }

        var error = await ReadErrorAsync(response);
        var message = error ?? $"Unable to create parent (Status: {(int)response.StatusCode} {response.StatusCode})";
        throw new InvalidOperationException(message);
    }

    public async Task<ParentDto?> FindParentByEmailAsync(string email)
    {
        var response = await _http.GetAsync($"parents?email={Uri.EscapeDataString(email)}");
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<ParentDto>();
        }

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        var error = await ReadErrorAsync(response);
        var message = error ?? $"Unable to lookup parent (Status: {(int)response.StatusCode} {response.StatusCode})";
        throw new InvalidOperationException(message);
    }

    public async Task<ParentDto?> GetParentAsync(Guid parentId)
    {
        return await _http.GetFromJsonAsync<ParentDto>($"parents/{parentId}");
    }

    public async Task<ParentDto?> GetCurrentParentAsync()
    {
        var response = await _http.GetAsync("parents/me");
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<ParentDto>();
        }

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        var error = await ReadErrorAsync(response);
        var message = error ?? $"Unable to load current parent (Status: {(int)response.StatusCode} {response.StatusCode})";
        throw new InvalidOperationException(message);
    }

    public async Task<IReadOnlyList<ChildDto>> GetChildrenAsync(Guid parentId)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, $"parents/{parentId}/children");
        req.Headers.Add("x-parent-id", parentId.ToString());
        var res = await _http.SendAsync(req);
        if (res.IsSuccessStatusCode)
        {
            var items = await res.Content.ReadFromJsonAsync<List<ChildDto>>();
            return items ?? new List<ChildDto>();
        }

        if (res.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return new List<ChildDto>();
        }

        var error = await ReadErrorAsync(res);
        var message = error ?? $"Unable to load children (Status: {(int)res.StatusCode} {res.StatusCode})";
        throw new InvalidOperationException(message);
    }

    public async Task<ChildDto> CreateChildAsync(Guid parentId, string name, decimal dollarPerPoint)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "children")
        {
            Content = JsonContent.Create(new CreateChildRequest(parentId, name, dollarPerPoint))
        };
        request.Headers.Add("x-parent-id", parentId.ToString());

        var response = await _http.SendAsync(request);
        if (response.IsSuccessStatusCode)
        {
            return (await response.Content.ReadFromJsonAsync<ChildDto>())!;
        }

        var error = await ReadErrorAsync(response);
        var message = error ?? $"Unable to create child (Status: {(int)response.StatusCode} {response.StatusCode})";
        throw new InvalidOperationException(message);
    }

    public async Task<ChildDto?> GetChildAsync(Guid childId, Guid parentId)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, $"children/{childId}?parentId={parentId}");
        req.Headers.Add("x-parent-id", parentId.ToString());
        var res = await _http.SendAsync(req);
        if (res.IsSuccessStatusCode)
        {
            return await res.Content.ReadFromJsonAsync<ChildDto>();
        }

        if (res.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        var error = await ReadErrorAsync(res);
        throw new InvalidOperationException(error ?? "Unable to fetch child");
    }

    public async Task<ChildDto?> UpdateChildAsync(Guid childId, Guid parentId, string name, decimal dollarPerPoint)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"children/{childId}")
        {
            Content = JsonContent.Create(new UpdateChildRequest(parentId, name, dollarPerPoint))
        };
        request.Headers.Add("x-parent-id", parentId.ToString());

        var response = await _http.SendAsync(request);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<ChildDto>();
        }

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        var error = await ReadErrorAsync(response);
        throw new InvalidOperationException(error ?? "Unable to update child");
    }

    public async Task DeleteChildAsync(Guid parentId, Guid childId)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, $"parents/{parentId}/children/{childId}");
        request.Headers.Add("x-parent-id", parentId.ToString());
        var response = await _http.SendAsync(request);
        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NoContent)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return;
            }

            var error = await ReadErrorAsync(response);
            throw new InvalidOperationException(error ?? "Unable to delete child");
        }
    }

    public async Task<BalanceDto?> GetChildBalanceAsync(Guid childId, Guid parentId)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, $"balances/{childId}?parentId={parentId}");
        req.Headers.Add("x-parent-id", parentId.ToString());
        var res = await _http.SendAsync(req);
        if (res.IsSuccessStatusCode)
        {
            return await res.Content.ReadFromJsonAsync<BalanceDto>();
        }

        if (res.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        var error = await ReadErrorAsync(res);
        throw new InvalidOperationException(error ?? "Unable to fetch balance");
    }

    public async Task<IReadOnlyList<DeedTypeDto>> GetDeedTypesAsync(Guid parentId)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, $"parents/{parentId}/deed-types");
        req.Headers.Add("x-parent-id", parentId.ToString());
        var res = await _http.SendAsync(req);
        if (res.IsSuccessStatusCode)
        {
            var items = await res.Content.ReadFromJsonAsync<List<DeedTypeDto>>();
            return items ?? new List<DeedTypeDto>();
        }

        if (res.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return new List<DeedTypeDto>();
        }

        var error = await ReadErrorAsync(res);
        throw new InvalidOperationException(error ?? "Unable to load deed types");
    }

    public async Task<DeedTypeDto> CreateDeedTypeAsync(Guid parentId, string name, int points)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "deed-types")
        {
            Content = JsonContent.Create(new CreateDeedTypeRequest(parentId, name, points))
        };
        request.Headers.Add("x-parent-id", parentId.ToString());

        var response = await _http.SendAsync(request);
        if (response.IsSuccessStatusCode)
        {
            return (await response.Content.ReadFromJsonAsync<DeedTypeDto>())!;
        }

        var error = await ReadErrorAsync(response);
        throw new InvalidOperationException(error ?? "Unable to create deed type");
    }

    public async Task DeleteDeedTypeAsync(Guid parentId, Guid deedTypeId)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, $"parents/{parentId}/deed-types/{deedTypeId}");
        request.Headers.Add("x-parent-id", parentId.ToString());
        var response = await _http.SendAsync(request);
        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NoContent)
        {
            var error = await ReadErrorAsync(response);
            throw new InvalidOperationException(error ?? "Unable to delete deed type");
        }
    }

    public async Task<IReadOnlyList<RedeemTypeDto>> GetRedeemTypesAsync(Guid parentId)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, $"parents/{parentId}/redeem-types");
        req.Headers.Add("x-parent-id", parentId.ToString());
        var res = await _http.SendAsync(req);
        if (res.IsSuccessStatusCode)
        {
            var items = await res.Content.ReadFromJsonAsync<List<RedeemTypeDto>>();
            return items ?? new List<RedeemTypeDto>();
        }

        if (res.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return new List<RedeemTypeDto>();
        }

        var error = await ReadErrorAsync(res);
        throw new InvalidOperationException(error ?? "Unable to load redeem types");
    }

    public async Task<RedeemTypeDto> CreateRedeemTypeAsync(Guid parentId, string name, int points)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "redeem-types")
        {
            Content = JsonContent.Create(new CreateRedeemTypeRequest(parentId, name, points))
        };
        request.Headers.Add("x-parent-id", parentId.ToString());

        var response = await _http.SendAsync(request);
        if (response.IsSuccessStatusCode)
        {
            return (await response.Content.ReadFromJsonAsync<RedeemTypeDto>())!;
        }

        var error = await ReadErrorAsync(response);
        var message = error ?? $"Unable to create redeem type (Status: {(int)response.StatusCode} {response.StatusCode})";
        throw new InvalidOperationException(message);
    }

    public async Task DeleteRedeemTypeAsync(Guid parentId, Guid redeemTypeId)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, $"parents/{parentId}/redeem-types/{redeemTypeId}");
        request.Headers.Add("x-parent-id", parentId.ToString());
        var response = await _http.SendAsync(request);
        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NoContent)
        {
            var error = await ReadErrorAsync(response);
            throw new InvalidOperationException(error ?? "Unable to delete redeem type");
        }
    }

    public async Task<DeedDto> CreateDeedAsync(Guid parentId, Guid childId, Guid deedTypeId, int? points, string? note, string? createdBy)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "deeds")
        {
            Content = JsonContent.Create(new CreateDeedRequest(parentId, childId, deedTypeId, points, note, createdBy))
        };
        request.Headers.Add("x-parent-id", parentId.ToString());

        var response = await _http.SendAsync(request);
        if (response.IsSuccessStatusCode)
        {
            return (await response.Content.ReadFromJsonAsync<DeedDto>())!;
        }

        var error = await ReadErrorAsync(response);
        var message = error ?? $"Unable to create deed (Status: {(int)response.StatusCode} {response.StatusCode})";
        throw new InvalidOperationException(message);
    }

    public async Task<IReadOnlyList<DeedDto>> GetDeedsForChildAsync(Guid childId, Guid parentId)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, $"children/{childId}/deeds?parentId={parentId}");
        req.Headers.Add("x-parent-id", parentId.ToString());
        var res = await _http.SendAsync(req);
        if (res.IsSuccessStatusCode)
        {
            var items = await res.Content.ReadFromJsonAsync<List<DeedDto>>();
            return items ?? new List<DeedDto>();
        }

        if (res.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return new List<DeedDto>();
        }

        var error = await ReadErrorAsync(res);
        throw new InvalidOperationException(error ?? "Unable to load deeds");
    }

    public async Task DeleteDeedAsync(Guid childId, Guid deedId, Guid parentId)
    {
        var req = new HttpRequestMessage(HttpMethod.Delete, $"children/{childId}/deeds/{deedId}?parentId={parentId}");
        req.Headers.Add("x-parent-id", parentId.ToString());
        var response = await _http.SendAsync(req);
        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NoContent)
        {
            var error = await ReadErrorAsync(response);
            var message = error ?? $"Unable to delete deed (Status: {(int)response.StatusCode} {response.StatusCode})";
            throw new InvalidOperationException(message);
        }
    }

    public async Task<RedemptionDto> CreateRedemptionAsync(Guid parentId, Guid childId, Guid redeemTypeId, string? description, string? createdBy)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "redemptions")
        {
            Content = JsonContent.Create(new CreateRedemptionRequest(parentId, childId, redeemTypeId, description, createdBy))
        };
        request.Headers.Add("x-parent-id", parentId.ToString());

        var response = await _http.SendAsync(request);
        if (response.IsSuccessStatusCode)
        {
            return (await response.Content.ReadFromJsonAsync<RedemptionDto>())!;
        }

        var error = await ReadErrorAsync(response);
        throw new InvalidOperationException(error ?? "Unable to create redemption");
    }

    public async Task<IReadOnlyList<RedemptionDto>> GetRedemptionsForChildAsync(Guid childId, Guid parentId)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, $"children/{childId}/redemptions?parentId={parentId}");
        req.Headers.Add("x-parent-id", parentId.ToString());
        var res = await _http.SendAsync(req);
        if (res.IsSuccessStatusCode)
        {
            var items = await res.Content.ReadFromJsonAsync<List<RedemptionDto>>();
            return items ?? new List<RedemptionDto>();
        }

        if (res.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return new List<RedemptionDto>();
        }

        var error = await ReadErrorAsync(res);
        throw new InvalidOperationException(error ?? "Unable to load redemptions");
    }

    public async Task<IReadOnlyList<ChildWithBalanceDto>> GetChildrenWithBalancesAsync(Guid parentId)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, $"parents/{parentId}/children/with-balances");
        req.Headers.Add("x-parent-id", parentId.ToString());
        var res = await _http.SendAsync(req);
        if (res.IsSuccessStatusCode)
        {
            var items = await res.Content.ReadFromJsonAsync<List<ChildWithBalanceDto>>();
            return items ?? new List<ChildWithBalanceDto>();
        }

        if (res.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return new List<ChildWithBalanceDto>();
        }

        var error = await ReadErrorAsync(res);
        var message = error ?? $"Unable to load children with balances (Status: {(int)res.StatusCode} {res.StatusCode})";
        throw new InvalidOperationException(message);
    }

    public async Task<AiKeyStatusDto> GetAiKeyStatusAsync()
    {
        var response = await _http.GetAsync("ai/key");
        if (response.IsSuccessStatusCode)
        {
            return (await response.Content.ReadFromJsonAsync<AiKeyStatusDto>()) ?? new AiKeyStatusDto(false);
        }

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return new AiKeyStatusDto(false);
        }

        var error = await ReadErrorAsync(response);
        var message = error ?? $"Unable to load AI key status (Status: {(int)response.StatusCode} {response.StatusCode})";
        throw new InvalidOperationException(message);
    }

    public async Task SetAiKeyAsync(string apiKey)
    {
        var response = await _http.PutAsJsonAsync("ai/key", new AiKeyRequest(apiKey));
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var error = await ReadErrorAsync(response);
        var message = error ?? $"Unable to save AI key (Status: {(int)response.StatusCode} {response.StatusCode})";
        throw new InvalidOperationException(message);
    }

    public async Task ClearAiKeyAsync()
    {
        var response = await _http.DeleteAsync("ai/key");
        if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NoContent)
        {
            return;
        }

        var error = await ReadErrorAsync(response);
        var message = error ?? $"Unable to clear AI key (Status: {(int)response.StatusCode} {response.StatusCode})";
        throw new InvalidOperationException(message);
    }

    public async Task<ParentDto> LinkParentAsync(string email)
    {
        var response = await _http.PostAsJsonAsync("parents/link", new LinkParentRequest(email));
        if (response.IsSuccessStatusCode)
        {
            return (await response.Content.ReadFromJsonAsync<ParentDto>())!;
        }

        var error = await ReadErrorAsync(response);
        var message = error ?? $"Unable to link parent (Status: {(int)response.StatusCode} {response.StatusCode})";
        throw new InvalidOperationException(message);
    }

    public async Task<InviteResponseDto> CreateParentInviteAsync(Guid parentId, string email, int? daysValid)
    {
        var response = await _http.PostAsJsonAsync($"parents/{parentId}/invites", new CreateParentInviteRequest(email, daysValid));
        if (response.IsSuccessStatusCode)
        {
            return (await response.Content.ReadFromJsonAsync<InviteResponseDto>())!;
        }

        var error = await ReadErrorAsync(response);
        var message = error ?? $"Unable to create invite (Status: {(int)response.StatusCode} {response.StatusCode})";
        throw new InvalidOperationException(message);
    }

    public async Task<IReadOnlyList<ParentInviteDto>> GetParentInvitesAsync(Guid parentId)
    {
        var response = await _http.GetAsync($"parents/{parentId}/invites");
        if (response.IsSuccessStatusCode)
        {
            return (await response.Content.ReadFromJsonAsync<List<ParentInviteDto>>()) ?? new List<ParentInviteDto>();
        }

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return new List<ParentInviteDto>();
        }

        var error = await ReadErrorAsync(response);
        var message = error ?? $"Unable to load invites (Status: {(int)response.StatusCode} {response.StatusCode})";
        throw new InvalidOperationException(message);
    }

    public async Task CancelParentInviteAsync(Guid parentId, Guid inviteId)
    {
        var response = await _http.DeleteAsync($"parents/{parentId}/invites/{inviteId}");
        if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return;
        }

        var error = await ReadErrorAsync(response);
        var message = error ?? $"Unable to cancel invite (Status: {(int)response.StatusCode} {response.StatusCode})";
        throw new InvalidOperationException(message);
    }

    public async Task<AcceptInviteResponse?> AcceptInviteAsync(string token)
    {
        var response = await _http.PostAsJsonAsync("invites/accept", new AcceptInviteRequest(token));
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<AcceptInviteResponse>();
        }

        var error = await ReadErrorAsync(response);
        var message = error ?? $"Unable to accept invite (Status: {(int)response.StatusCode} {response.StatusCode})";
        throw new InvalidOperationException(message);
    }

    public async Task<ProfileDto?> GetProfileAsync()
    {
        var response = await _http.GetAsync("profile");
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<ProfileDto>();
        }

        var error = await ReadErrorAsync(response);
        var message = error ?? $"Unable to load profile (Status: {(int)response.StatusCode} {response.StatusCode})";
        throw new InvalidOperationException(message);
    }

    public async Task<ProfileDto> UpdateProfileAsync(string displayName)
    {
        var response = await _http.PostAsJsonAsync("profile", new UpdateProfileRequest(displayName));
        if (response.IsSuccessStatusCode)
        {
            return (await response.Content.ReadFromJsonAsync<ProfileDto>())!;
        }

        var error = await ReadErrorAsync(response);
        var message = error ?? $"Unable to update profile (Status: {(int)response.StatusCode} {response.StatusCode})";
        throw new InvalidOperationException(message);
    }

    private static async Task<string?> ReadErrorAsync(HttpResponseMessage response)
    {
        try
        {
            var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
            return error?.Error;
        }
        catch
        {
            try
            {
                var raw = await response.Content.ReadAsStringAsync();
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    return raw.Trim();
                }
            }
            catch
            {
                // Ignore secondary failures to avoid masking the original error.
            }

            return null;
        }
    }
}
