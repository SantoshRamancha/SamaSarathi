namespace Samasarathi.Application.Abstractions;

public static class AppRoles
{
    public const string Driver = "driver";
    public const string Rider = "rider";

    /// <summary>Our verification team. Only phone numbers listed in config (Admin:Phones) can log in as admin.</summary>
    public const string Admin = "admin";
}

public enum OtpSendResult { Sent, TooSoon }

/// <summary>One-time login codes sent by SMS.</summary>
public interface IOtpService
{
    Task<OtpSendResult> SendAsync(string phone, CancellationToken ct = default);

    /// <summary>True once for a correct, unexpired code; the code is then used up.</summary>
    Task<bool> VerifyAsync(string phone, string code, CancellationToken ct = default);
}

public interface ISmsSender
{
    Task SendAsync(string phone, string message, CancellationToken ct = default);
}

public interface ITokenService
{
    string CreateToken(string phone, string role);
}

public interface IFileStorage
{
    /// <summary>Saves the file and returns a key to find it again.</summary>
    Task<string> SaveAsync(Stream content, string extension, CancellationToken ct = default);

    /// <summary>Null if the file is gone.</summary>
    Task<Stream?> OpenReadAsync(string key, CancellationToken ct = default);
    Task DeleteAsync(string key, CancellationToken ct = default);
}
