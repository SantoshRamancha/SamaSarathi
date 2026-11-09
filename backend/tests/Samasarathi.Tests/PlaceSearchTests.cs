using System.Net;
using Samasarathi.Application.Abstractions;
using Samasarathi.Infrastructure.Places;

namespace Samasarathi.Tests;

public class PlaceSearchTests
{
    private const string TwoPlaces = """
        [
          {"name":"HITEC City Metro Station","display_name":"HITEC City Metro Station, Madhapur, Hyderabad, Telangana, India","lat":"17.4435","lon":"78.3772"},
          {"name":"","display_name":"Plot 12, Road No 3, Banjara Hills, Hyderabad, India","lat":"17.4126","lon":"78.4482"}
        ]
        """;

    private sealed class FakeHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public string? LastUrl { get; private set; }
        public string? LastUserAgent { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            LastUrl = request.RequestUri!.AbsoluteUri;
            LastUserAgent = request.Headers.UserAgent.ToString();
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
        }
    }

    private static (NominatimPlaceSearch Search, FakeHandler Handler) Make(HttpStatusCode status = HttpStatusCode.OK, string body = TwoPlaces)
    {
        var handler = new FakeHandler(status, body);
        // No spacing between calls in tests: the 1-request-per-second gate is real-time behaviour.
        var options = new PlaceSearchOptions { MinMillisecondsBetweenRequests = 0, UserAgent = "Samasarathi-tests/1.0" };
        return (new NominatimPlaceSearch(new HttpClient(handler), options, TimeProvider.System), handler);
    }

    // --- parsing -----------------------------------------------------------------------------

    [Fact]
    public void Parse_reads_name_address_and_coordinates()
    {
        var places = NominatimPlaceSearch.Parse(TwoPlaces, 5);

        Assert.Equal(2, places.Count);
        Assert.Equal("HITEC City Metro Station", places[0].Name);
        Assert.StartsWith("HITEC City Metro Station, Madhapur", places[0].Address);
        Assert.Equal(17.4435, places[0].Latitude, 4);
        Assert.Equal(78.3772, places[0].Longitude, 4);
    }

    [Fact]
    public void Parse_uses_the_first_part_of_the_address_when_there_is_no_name()
    {
        var places = NominatimPlaceSearch.Parse(TwoPlaces, 5);
        Assert.Equal("Plot 12", places[1].Name);
    }

    [Fact]
    public void Parse_respects_the_limit()
    {
        Assert.Single(NominatimPlaceSearch.Parse(TwoPlaces, 1));
    }

    [Fact]
    public void Parse_skips_entries_without_usable_coordinates()
    {
        const string json = """
            [
              {"name":"No coordinates","display_name":"x"},
              {"name":"Bad latitude","display_name":"y","lat":"95","lon":"78"},
              {"name":"Not a number","display_name":"z","lat":"abc","lon":"78"},
              {"name":"Good","display_name":"Good, Hyderabad","lat":"17.4","lon":"78.4"}
            ]
            """;

        var places = NominatimPlaceSearch.Parse(json, 5);

        Assert.Single(places);
        Assert.Equal("Good", places[0].Name);
    }

    [Fact]
    public void Parse_accepts_numeric_coordinates_too()
    {
        var places = NominatimPlaceSearch.Parse("""[{"name":"A","display_name":"A, B","lat":17.4,"lon":78.4}]""", 5);
        Assert.Single(places);
    }

    [Fact]
    public void Parse_of_an_empty_result_is_empty() =>
        Assert.Empty(NominatimPlaceSearch.Parse("[]", 5));

    [Fact]
    public void Parse_of_something_that_is_not_json_is_reported_as_unavailable() =>
        Assert.Throws<PlaceSearchUnavailableException>(() => NominatimPlaceSearch.Parse("<html>oops</html>", 5));

    // --- request ------------------------------------------------------------------------------

    [Fact]
    public async Task Search_sends_the_query_the_country_and_a_user_agent_and_prefers_nearby_places()
    {
        var (search, handler) = Make();

        await search.SearchAsync("hitec city & metro", 17.40, 78.45);

        var url = handler.LastUrl!;
        Assert.Contains("q=hitec%20city%20%26%20metro", url);
        Assert.Contains("countrycodes=in", url);
        Assert.Contains("viewbox=77.95,17.9,78.95,16.9", url);
        Assert.DoesNotContain("bounded", url); // ranks nearby higher but still finds places further away
        Assert.Equal("Samasarathi-tests/1.0", handler.LastUserAgent);
    }

    [Fact]
    public async Task Search_without_a_position_has_no_viewbox()
    {
        var (search, handler) = Make();

        await search.SearchAsync("charminar", null, null);

        Assert.DoesNotContain("viewbox", handler.LastUrl!);
    }

    [Fact]
    public async Task Search_too_short_to_be_useful_returns_nothing_without_calling_out()
    {
        var (search, handler) = Make();

        Assert.Empty(await search.SearchAsync(" a ", 17.4, 78.4));
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Repeating_a_search_from_the_same_area_uses_the_cache()
    {
        var (search, handler) = Make();

        await search.SearchAsync("Charminar", 17.401, 78.451);
        var again = await search.SearchAsync("  charminar ", 17.402, 78.452);

        Assert.Equal(1, handler.Calls);
        Assert.Equal(2, again.Count);
    }

    [Fact]
    public async Task A_different_search_is_not_served_from_the_cache()
    {
        var (search, handler) = Make();

        await search.SearchAsync("charminar", 17.4, 78.4);
        await search.SearchAsync("golconda", 17.4, 78.4);

        Assert.Equal(2, handler.Calls);
    }

    // --- failures -----------------------------------------------------------------------------

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task An_error_from_the_search_service_is_reported_as_unavailable(HttpStatusCode status)
    {
        var (search, _) = Make(status, "nope");

        await Assert.ThrowsAsync<PlaceSearchUnavailableException>(() => search.SearchAsync("charminar", 17.4, 78.4));
    }

    [Fact]
    public async Task A_failed_search_is_not_cached()
    {
        var (search, handler) = Make(HttpStatusCode.InternalServerError, "nope");

        await Assert.ThrowsAsync<PlaceSearchUnavailableException>(() => search.SearchAsync("charminar", 17.4, 78.4));
        await Assert.ThrowsAsync<PlaceSearchUnavailableException>(() => search.SearchAsync("charminar", 17.4, 78.4));

        Assert.Equal(2, handler.Calls);
    }
}
