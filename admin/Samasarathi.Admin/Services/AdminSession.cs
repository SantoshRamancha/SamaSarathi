using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace Samasarathi.Admin.Services;

/// <summary>
/// The logged-in admin for one browser tab. Blazor Server keeps one instance per connection ("circuit"),
/// and the token is also saved, encrypted, in the tab's sessionStorage so a page refresh keeps you logged in.
/// Closing the tab logs you out.
/// </summary>
public sealed class AdminSession(ProtectedSessionStorage storage)
{
    private const string StorageKey = "admin-session";

    private sealed record Saved(string Token, string Phone);

    public string? Token { get; private set; }
    public string? Phone { get; private set; }
    public bool IsLoggedIn => Token is not null;

    /// <summary>Reads the saved login. Only works once the page is interactive (after first render).</summary>
    public async Task RestoreAsync()
    {
        try
        {
            var result = await storage.GetAsync<Saved>(StorageKey);
            if (result is { Success: true, Value: { } saved })
                (Token, Phone) = (saved.Token, saved.Phone);
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            // Saved by an older run with different encryption keys: treat as logged out.
            await storage.DeleteAsync(StorageKey);
        }
    }

    public async Task SaveAsync(string token, string phone)
    {
        (Token, Phone) = (token, phone);
        await storage.SetAsync(StorageKey, new Saved(token, phone));
    }

    public async Task ClearAsync()
    {
        (Token, Phone) = (null, null);
        await storage.DeleteAsync(StorageKey);
    }
}
