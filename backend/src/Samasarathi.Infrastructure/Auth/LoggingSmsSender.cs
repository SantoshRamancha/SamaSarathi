using Microsoft.Extensions.Logging;
using Samasarathi.Application.Abstractions;

namespace Samasarathi.Infrastructure.Auth;

/// <summary>Development stand-in: writes the SMS to the API console instead of sending it.
/// Replace with an MSG91/Twilio sender before going live.</summary>
public sealed class LoggingSmsSender(ILogger<LoggingSmsSender> logger) : ISmsSender
{
    public Task SendAsync(string phone, string message, CancellationToken ct = default)
    {
        logger.LogWarning("DEV SMS to {Phone}: {Message}", phone, message);
        return Task.CompletedTask;
    }
}
