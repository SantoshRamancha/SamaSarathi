namespace Samasarathi.Application.Abstractions;

/// <summary>"Rides" config section.</summary>
public sealed class RideOptions
{
    /// <summary>How long a driver has to accept a request before it lapses.</summary>
    public int OfferTimeoutSeconds { get; init; } = 45;

    public TimeSpan OfferTimeout => TimeSpan.FromSeconds(OfferTimeoutSeconds);
}
