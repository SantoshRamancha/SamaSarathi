using Samasarathi.Application.Contracts;
using Samasarathi.Domain;

namespace Samasarathi.Tests;

public class SupportInfoTests
{
    [Fact]
    public void Nothing_configured_means_support_is_not_available()
    {
        var info = SupportInfo.From(new SupportOptions());

        Assert.False(info.Available);
        Assert.Null(info.Phone);
        Assert.Null(info.WhatsApp);
        Assert.Null(info.Email);
    }

    [Fact]
    public void Blank_values_are_treated_as_not_configured()
    {
        var info = SupportInfo.From(new SupportOptions { Phone = "", WhatsApp = "   ", Email = null, Hours = " " });

        Assert.False(info.Available);
        Assert.Null(info.WhatsApp);
        Assert.Null(info.Hours);
    }

    [Fact]
    public void Configured_values_are_trimmed_and_passed_on()
    {
        var info = SupportInfo.From(new SupportOptions
        {
            Phone = " +919876543210 ", WhatsApp = "+919876543210", Email = "help@samasarathi.example", Hours = "Mon-Sat 9-7",
        });

        Assert.True(info.Available);
        Assert.Equal("+919876543210", info.Phone);
        Assert.Equal("help@samasarathi.example", info.Email);
        Assert.Equal("Mon-Sat 9-7", info.Hours);
    }

    [Fact]
    public void Any_one_way_to_reach_us_is_enough()
    {
        Assert.True(SupportInfo.From(new SupportOptions { Email = "help@samasarathi.example" }).Available);
        Assert.True(SupportInfo.From(new SupportOptions { WhatsApp = "+919876543210" }).Available);
    }
}
