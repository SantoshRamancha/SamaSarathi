using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Samasarathi.Application.Abstractions;
using Samasarathi.Application.Contracts;
using Samasarathi.Domain;
using Samasarathi.Domain.Entities;
using Samasarathi.Infrastructure.Persistence;

namespace Samasarathi.Api.Controllers;

/// <summary>Driver verification for our team: review documents, record each check, then approve or reject.</summary>
[ApiController]
[Route("api/admin/drivers")]
[Authorize(Roles = AppRoles.Admin)]
public class AdminDriversController(AppDbContext db, IFileStorage files, TimeProvider clock) : ControllerBase
{
    private const int MaxResults = 200;

    private string Reviewer => User.FindFirst("phone")!.Value;

    [HttpGet("counts")]
    public async Task<AdminDriverCounts> Counts(CancellationToken ct)
    {
        var counts = await db.Drivers
            .GroupBy(d => d.VerificationStatus)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, ct);

        int Of(VerificationStatus s) => counts.GetValueOrDefault(s);
        return new AdminDriverCounts(
            Of(VerificationStatus.Pending), Of(VerificationStatus.InReview), Of(VerificationStatus.Approved),
            Of(VerificationStatus.Rejected), Of(VerificationStatus.Suspended));
    }

    /// <summary>Drivers in one status. The review queue (InReview) lists the longest-waiting first.</summary>
    /// <param name="status">Defaults to InReview. When searching without a status, every status is searched.</param>
    /// <param name="search">Part of a name, phone number or vehicle number.</param>
    [HttpGet]
    public async Task<IReadOnlyList<AdminDriverSummary>> List(
        VerificationStatus? status = null, string? search = null, CancellationToken ct = default)
    {
        var searching = !string.IsNullOrWhiteSpace(search);
        if (status is null && !searching) status = VerificationStatus.InReview;

        var query = db.Drivers.Include(d => d.Vehicle).Include(d => d.VerificationChecks).AsSplitQuery().AsQueryable();
        if (status is { } s) query = query.Where(d => d.VerificationStatus == s);

        if (searching)
        {
            var text = $"%{search!.Trim()}%";
            var compact = $"%{new string(search.Where(char.IsAsciiLetterOrDigit).ToArray()).ToUpperInvariant()}%";
            query = query.Where(d => EF.Functions.ILike(d.FullName, text)
                || EF.Functions.Like(d.Phone, compact)
                || (d.Vehicle != null && EF.Functions.Like(d.Vehicle.RegistrationNumber, compact)));
        }

        query = status == VerificationStatus.InReview
            ? query.OrderBy(d => d.SubmittedAt)
            : query.OrderByDescending(d => d.ReviewedAt ?? d.SubmittedAt ?? d.CreatedAt);

        var drivers = await query.Take(MaxResults).ToListAsync(ct);
        return drivers.Select(d =>
        {
            var checks = d.CurrentChecks();
            return new AdminDriverSummary(
                d.Id, d.FullName, d.Phone, d.Vehicle?.Type, d.Vehicle?.RegistrationNumber, d.RatePerKm,
                d.VerificationStatus, d.CreatedAt, d.SubmittedAt,
                checks.Count(c => c.Result == CheckResult.Passed), checks.Count(c => c.Result == CheckResult.Failed),
                checks.Count);
        }).ToList();
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AdminDriverDetail>> Get(Guid id, CancellationToken ct) =>
        await Load(id, ct) is { } driver ? Detail(driver) : NotFound();

    /// <summary>The uploaded file itself, shown inline in the portal.</summary>
    [HttpGet("{id:guid}/documents/{type}")]
    public async Task<IActionResult> Document(Guid id, DocumentType type, CancellationToken ct)
    {
        var doc = await db.DriverDocuments.FirstOrDefaultAsync(d => d.DriverId == id && d.Type == type, ct);
        if (doc is null) return NotFound();

        var stream = await files.OpenReadAsync(doc.StorageKey, ct);
        if (stream is null) return Problem(detail: "The file is missing from storage.", statusCode: 404);

        // Uploaded files are never trusted to run in the browser: no sniffing, no scripts.
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.ContentSecurityPolicy = "default-src 'none'; img-src 'self'; style-src 'unsafe-inline'";
        return File(stream, doc.ContentType, enableRangeProcessing: true);
    }

    [HttpPut("{id:guid}/checks/{type}")]
    public Task<ActionResult<AdminDriverDetail>> RecordCheck(
        Guid id, VerificationCheckType type, RecordCheckRequest req, CancellationToken ct) =>
        Review(id, d => d.RecordCheck(type, req.Result, req.Notes, Reviewer, clock.GetUtcNow()), ct);

    [HttpPost("{id:guid}/approve")]
    public Task<ActionResult<AdminDriverDetail>> Approve(Guid id, CancellationToken ct) =>
        Review(id, d => d.Approve(Reviewer, clock.GetUtcNow()), ct);

    [HttpPost("{id:guid}/reject")]
    public Task<ActionResult<AdminDriverDetail>> Reject(Guid id, RejectRequest req, CancellationToken ct) =>
        Review(id, d => d.Reject(req.Reason, Reviewer, clock.GetUtcNow()), ct);

    /// <summary>Loads the driver, applies one review rule from the Domain, and saves.</summary>
    private async Task<ActionResult<AdminDriverDetail>> Review(Guid id, Action<Driver> action, CancellationToken ct)
    {
        if (await Load(id, ct) is not { } driver) return NotFound();
        try
        {
            action(driver);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: 400);
        }

        await db.SaveChangesAsync(ct);
        return Detail(driver);
    }

    private Task<Driver?> Load(Guid id, CancellationToken ct) =>
        db.Drivers
            .Include(d => d.Vehicle)
            .Include(d => d.Documents)
            .Include(d => d.VerificationChecks)
            .AsSplitQuery()
            .FirstOrDefaultAsync(d => d.Id == id, ct);

    private static AdminDriverDetail Detail(Driver d)
    {
        var current = d.CurrentChecks();
        return new AdminDriverDetail(
            d.Id, d.FullName, d.Phone, d.RatePerKm, d.VerificationStatus,
            d.CreatedAt, d.SubmittedAt, d.ReviewedAt, d.ReviewedBy, d.RejectionReason,
            d.Vehicle?.Type, d.Vehicle?.RegistrationNumber, d.Vehicle?.Model,
            d.Documents.OrderBy(x => x.Type)
                .Select(x => new AdminDocument(x.Type, x.OriginalFileName, x.ContentType, x.SizeBytes, x.UploadedAt)).ToList(),
            current.Select(ToDto).ToList(),
            d.VerificationChecks.Except(current).OrderByDescending(c => c.CreatedAt).ThenBy(c => c.Type).Select(ToDto).ToList());
    }

    private static AdminCheck ToDto(VerificationCheck c) =>
        new(c.Type, c.Result, c.Notes, c.Provider, c.CheckedAt, c.CreatedAt);
}
