namespace Samasarathi.Domain.Entities;

/// <summary>A file the driver uploaded during registration (photo, licence, RC, ID).</summary>
public class DriverDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DriverId { get; set; }
    public DocumentType Type { get; set; }

    /// <summary>Where the file lives in file storage. Never shown to clients.</summary>
    public required string StorageKey { get; set; }
    public required string OriginalFileName { get; set; }
    public required string ContentType { get; set; }
    public long SizeBytes { get; set; }
    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;
}
