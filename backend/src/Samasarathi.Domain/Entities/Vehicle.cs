namespace Samasarathi.Domain.Entities;

public class Vehicle
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DriverId { get; set; }
    public VehicleType Type { get; set; }
    public required string RegistrationNumber { get; set; }
    public string? Model { get; set; }
}
