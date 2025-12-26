using Microsoft.JSInterop;

namespace GoodDeeds.Client.Services;

public class UserSettingsService
{
    private const string ParentIdKey = "good-deeds.parent-id";
    private const string ParentEmailKey = "good-deeds.parent-email";
    private const string ChatGptKey = "good-deeds.chatgpt-key";

    private readonly IJSRuntime _jsRuntime;
    private Guid? _cachedParentId;
    private string? _cachedParentEmail;
    private string? _cachedChatGptKey;

    public UserSettingsService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task<Guid?> GetParentIdAsync()
    {
        if (_cachedParentId.HasValue)
        {
            return _cachedParentId.Value;
        }

        try
        {
            var value = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", ParentIdKey);
            if (Guid.TryParse(value, out var id))
            {
                _cachedParentId = id;
                return id;
            }
        }
        catch
        {
            // Gracefully handle environments where localStorage is unavailable/restricted
        }

        return null;
    }

    public async Task SetParentIdAsync(Guid id)
    {
        _cachedParentId = id;
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", ParentIdKey, id.ToString());
        }
        catch
        {
            // Ignore storage errors
        }
    }

    public async Task ClearParentIdAsync()
    {
        _cachedParentId = null;
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", ParentIdKey);
        }
        catch
        {
            // Ignore storage errors
        }
    }

    public async Task<string?> GetParentEmailAsync()
    {
        if (!string.IsNullOrWhiteSpace(_cachedParentEmail))
        {
            return _cachedParentEmail;
        }

        try
        {
            var value = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", ParentEmailKey);
            _cachedParentEmail = string.IsNullOrWhiteSpace(value) ? null : value;
        }
        catch
        {
            _cachedParentEmail = null;
        }

        return _cachedParentEmail;
    }

    public async Task SetParentEmailAsync(string? email)
    {
        _cachedParentEmail = string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
        if (_cachedParentEmail is null)
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", ParentEmailKey);
            }
            catch
            {
                // Ignore storage errors
            }
        }
        else
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", ParentEmailKey, _cachedParentEmail);
            }
            catch
            {
                // Ignore storage errors
            }
        }
    }

    public async Task ClearParentEmailAsync()
    {
        _cachedParentEmail = null;
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", ParentEmailKey);
        }
        catch
        {
            // Ignore storage errors
        }
    }

    public async Task<string?> GetChatGptKeyAsync()
    {
        if (_cachedChatGptKey is not null)
        {
            return _cachedChatGptKey;
        }

        try
        {
            var value = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", ChatGptKey);
            _cachedChatGptKey = string.IsNullOrWhiteSpace(value) ? null : value;
        }
        catch
        {
            _cachedChatGptKey = null;
        }
        return _cachedChatGptKey;
    }

    public async Task SetChatGptKeyAsync(string? key)
    {
        _cachedChatGptKey = string.IsNullOrWhiteSpace(key) ? null : key?.Trim();
        if (_cachedChatGptKey is null)
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", ChatGptKey);
            }
            catch
            {
                // Ignore storage errors
            }
        }
        else
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", ChatGptKey, _cachedChatGptKey);
            }
            catch
            {
                // Ignore storage errors
            }
        }
    }
}
