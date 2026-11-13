using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Components;
using Samasarathi.Application.Contracts;
using Samasarathi.Domain;

namespace Samasarathi.Admin.Services;

public sealed class ApiException(string message, HttpStatusCode status) : Exception(message)
{
    public HttpStatusCode Status { get; } = status;
}

public sealed record AuthResult(string Token, string Phone);

public sealed record DocumentFile(byte[] Bytes, string ContentType);

/// <summary>Calls the Samasarathi API with the admin's token. Every failure becomes an <see cref="ApiException"/>
/// with a message that can be shown as-is.</summary>
public sealed class ApiClient(HttpClient http, AdminSession session, NavigationManager nav)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task RequestOtpAsync(string phone) =>
        await SendAsync(new HttpRequestMessage(HttpMethod.Post, "api/auth/otp/request") { Content = Body(new { phone }) });

    public async Task<AuthResult> VerifyOtpAsync(string phone, string code) =>
        await ReadAsync<AuthResult>(new HttpRequestMessage(HttpMethod.Post, "api/auth/otp/verify")
        {
            Content = Body(new { phone, code, role = "admin" }),
        });

    public Task<AdminDriverCounts> CountsAsync() => GetAsync<AdminDriverCounts>("api/admin/drivers/counts");

    /// <param name="status">Null with a search term searches every status.</param>
    public Task<List<AdminDriverSummary>> DriversAsync(VerificationStatus? status, string? search) =>
        GetAsync<List<AdminDriverSummary>>(
            $"api/admin/drivers?status={status}&search={Uri.EscapeDataString(search ?? "")}");

    public Task<AdminDriverDetail> DriverAsync(Guid id) => GetAsync<AdminDriverDetail>($"api/admin/drivers/{id}");

    public async Task<DocumentFile> DocumentAsync(Guid id, DocumentType type)
    {
        using var response = await SendAsync(Authorized(HttpMethod.Get, $"api/admin/drivers/{id}/documents/{type}"));
        return new DocumentFile(
            await response.Content.ReadAsByteArrayAsync(),
            response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream");
    }

    public Task<AdminDriverDetail> RecordCheckAsync(Guid id, VerificationCheckType type, CheckResult result, string? notes) =>
        ReadAsync<AdminDriverDetail>(Authorized(HttpMethod.Put, $"api/admin/drivers/{id}/checks/{type}",
            new RecordCheckRequest(result, notes)));

    public Task<AdminDriverDetail> ApproveAsync(Guid id) =>
        ReadAsync<AdminDriverDetail>(Authorized(HttpMethod.Post, $"api/admin/drivers/{id}/approve"));

    public Task<AdminDriverDetail> RejectAsync(Guid id, string reason) =>
        ReadAsync<AdminDriverDetail>(Authorized(HttpMethod.Post, $"api/admin/drivers/{id}/reject", new RejectRequest(reason)));

    public Task<AdminDriverDetail> SuspendAsync(Guid id, string reason) =>
        ReadAsync<AdminDriverDetail>(Authorized(HttpMethod.Post, $"api/admin/drivers/{id}/suspend", new SuspendRequest(reason)));

    public Task<AdminDriverDetail> ReinstateAsync(Guid id) =>
        ReadAsync<AdminDriverDetail>(Authorized(HttpMethod.Post, $"api/admin/drivers/{id}/reinstate"));

    public Task<AdminSubscriptionCounts> SubscriptionCountsAsync() => GetAsync<AdminSubscriptionCounts>("api/admin/subscriptions/counts");

    public Task<AdminSubscriptionSettings> SubscriptionSettingsAsync() =>
        GetAsync<AdminSubscriptionSettings>("api/admin/subscriptions/settings");

    public Task<List<AdminSubscriptionSummary>> SubscriptionsAsync(AdminSubscriptionFilter filter, string? search) =>
        GetAsync<List<AdminSubscriptionSummary>>(
            $"api/admin/subscriptions?filter={filter}&search={Uri.EscapeDataString(search ?? "")}");

    public Task<AdminSubscriptionSummary> GrantSubscriptionAsync(Guid driverId, int days, string note) =>
        ReadAsync<AdminSubscriptionSummary>(Authorized(HttpMethod.Post, $"api/admin/subscriptions/{driverId}/grant",
            new GrantSubscriptionRequest(days, note)));

    public Task<AdminRideCounts> RideCountsAsync() => GetAsync<AdminRideCounts>("api/admin/rides/counts");

    public Task<List<AdminRideSummary>> RidesAsync(AdminRideFilter filter, string? search) =>
        GetAsync<List<AdminRideSummary>>(
            $"api/admin/rides?filter={filter}&search={Uri.EscapeDataString(search ?? "")}&take=100");

    public Task<AdminRideDetail> RideAsync(Guid id) => GetAsync<AdminRideDetail>($"api/admin/rides/{id}");

    private Task<T> GetAsync<T>(string url) => ReadAsync<T>(Authorized(HttpMethod.Get, url));

    private HttpRequestMessage Authorized(HttpMethod method, string url, object? body = null)
    {
        var request = new HttpRequestMessage(method, url) { Content = body is null ? null : Body(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.Token);
        return request;
    }

    private static JsonContent Body(object value) => JsonContent.Create(value, value.GetType(), options: Json);

    private async Task<T> ReadAsync<T>(HttpRequestMessage request)
    {
        using var response = await SendAsync(request);
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
    {
        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request);
        }
        catch (HttpRequestException)
        {
            throw new ApiException("Can't reach the Samasarathi API. Is it running?", HttpStatusCode.ServiceUnavailable);
        }

        if (response.IsSuccessStatusCode) return response;

        var message = await ProblemMessageAsync(response);
        // An expired admin token: log out and go to the login page. Login itself also returns 401 for a wrong code.
        if (response.StatusCode == HttpStatusCode.Unauthorized && request.Headers.Authorization is not null)
        {
            await session.ClearAsync();
            nav.NavigateTo("login");
            message = "Your session has expired. Please log in again.";
        }
        response.Dispose();
        throw new ApiException(message, response.StatusCode);
    }

    /// <summary>ASP.NET errors come as ProblemDetails JSON: use "detail", else the first validation error, else "title".</summary>
    private static async Task<string> ProblemMessageAsync(HttpResponseMessage response)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
            if (problem.TryGetProperty("detail", out var detail) && detail.GetString() is { Length: > 0 } d) return d;
            if (problem.TryGetProperty("errors", out var errors))
                foreach (var field in errors.EnumerateObject())
                    if (field.Value.EnumerateArray().FirstOrDefault().GetString() is { Length: > 0 } e) return e;
            if (problem.TryGetProperty("title", out var title) && title.GetString() is { Length: > 0 } t) return t;
        }
        catch (JsonException)
        {
        }
        return response.StatusCode == HttpStatusCode.Forbidden
            ? "You don't have access to this."
            : $"Something went wrong ({(int)response.StatusCode}).";
    }
}
