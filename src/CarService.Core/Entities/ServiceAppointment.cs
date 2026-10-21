using CarService.Core.Common;

namespace CarService.Core.Entities;

public enum AppointmentStatus
{
    Scheduled,
    InProgress,
    Completed,
    Cancelled
}

public class ServiceAppointment
{
    public int Id { get; set; }

    public DateTime ScheduledAt { get; set; }
    public required string Description { get; set; }   // "Oil change + brake check"
    public decimal? Cost { get; set; }                  // decimal for money, NEVER double/float
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Scheduled;

    public int VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }

    // ---- Domain logic ------------------------------------------------------------
    // WHAT: rules about this thing live ON the thing. This is the payoff of a Core
    //       project that has no framework dependencies: these rules can be unit-tested
    //       with plain `new ServiceAppointment { ... }` - no database, no web server.
    // NOTE: These are get-only computed properties, so EF Core does not map them to columns.

    // The state machine:  Scheduled -> InProgress -> Completed
    //                          \            \
    //                           +---------> Cancelled        (Completed/Cancelled are final)
    public IReadOnlyList<AppointmentStatus> AllowedNextStatuses => Status switch
    {
        AppointmentStatus.Scheduled  => [AppointmentStatus.InProgress, AppointmentStatus.Cancelled],
        AppointmentStatus.InProgress => [AppointmentStatus.Completed, AppointmentStatus.Cancelled],
        _                            => []
    };

    public bool IsClosed => Status is AppointmentStatus.Completed or AppointmentStatus.Cancelled;

    // Applies the change only if every rule passes; on failure the object is left untouched.
    public OperationResult ChangeStatus(AppointmentStatus next, decimal? cost = null)
    {
        if (!AllowedNextStatuses.Contains(next))
            return OperationResult.Fail($"Cannot move an appointment from {Status} to {next}.");

        var finalCost = cost ?? Cost;
        if (next == AppointmentStatus.Completed && finalCost is null)
            return OperationResult.Fail("Enter the final cost before completing the appointment.");

        Cost = finalCost;
        Status = next;
        return OperationResult.Ok();
    }
}
