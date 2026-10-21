namespace CarService.Core.Entities;

public enum AppointmentStatus
{
    Scheduled,
    InProgress,
    Completed,
    Cancelled
}

// Enums are stored as integers by default. That's fine to start; later you can
// make EF store the name instead (HasConversion<string>()).
public class ServiceAppointment
{
    public int Id { get; set; }

    public DateTime ScheduledAt { get; set; }
    public required string Description { get; set; }   // "Oil change + brake check"
    public decimal? Cost { get; set; }                  // decimal for money, NEVER double/float
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Scheduled;

    public int VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }
}
