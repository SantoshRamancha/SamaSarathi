namespace Samasarathi.Domain.Entities;

public class Rider
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string FullName { get; set; }
    public required string Phone { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
