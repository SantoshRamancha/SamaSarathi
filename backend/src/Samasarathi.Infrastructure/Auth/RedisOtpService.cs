using System.Security.Cryptography;
using System.Text;
using Samasarathi.Application.Abstractions;
using StackExchange.Redis;

namespace Samasarathi.Infrastructure.Auth;

public sealed class OtpOptions
{
    /// <summary>Fixed code for local development only. Leave empty in production.</summary>
    public string? DevCode { get; init; }
    public int ExpiryMinutes { get; init; } = 5;
    public int ResendCooldownSeconds { get; init; } = 30;
    public int MaxAttempts { get; init; } = 5;
}

/// <summary>Codes live in Redis with an expiry, so they vanish on their own.</summary>
public sealed class RedisOtpService(IConnectionMultiplexer redis, ISmsSender sms, OtpOptions options) : IOtpService
{
    private static string CodeKey(string phone) => $"otp:code:{phone}";
    private static string AttemptsKey(string phone) => $"otp:attempts:{phone}";
    private static string CooldownKey(string phone) => $"otp:cooldown:{phone}";

    public async Task<OtpSendResult> SendAsync(string phone, CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        var allowed = await db.StringSetAsync(CooldownKey(phone), 1,
            TimeSpan.FromSeconds(options.ResendCooldownSeconds), When.NotExists);
        if (!allowed) return OtpSendResult.TooSoon;

        var code = string.IsNullOrEmpty(options.DevCode)
            ? RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6")
            : options.DevCode;

        await db.StringSetAsync(CodeKey(phone), code, TimeSpan.FromMinutes(options.ExpiryMinutes));
        await db.KeyDeleteAsync(AttemptsKey(phone));

        await sms.SendAsync(phone, $"{code} is your Samasarathi login code. Valid for {options.ExpiryMinutes} minutes.", ct);
        return OtpSendResult.Sent;
    }

    public async Task<bool> VerifyAsync(string phone, string code, CancellationToken ct = default)
    {
        var db = redis.GetDatabase();

        // Limit guesses: after MaxAttempts wrong tries the code is thrown away.
        var attempts = await db.StringIncrementAsync(AttemptsKey(phone));
        await db.KeyExpireAsync(AttemptsKey(phone), TimeSpan.FromMinutes(options.ExpiryMinutes));
        if (attempts > options.MaxAttempts)
        {
            await db.KeyDeleteAsync(CodeKey(phone));
            return false;
        }

        var stored = await db.StringGetAsync(CodeKey(phone));
        if (stored.IsNullOrEmpty
            || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(stored.ToString()), Encoding.UTF8.GetBytes(code)))
            return false;

        await db.KeyDeleteAsync([CodeKey(phone), AttemptsKey(phone)]);
        return true;
    }
}
