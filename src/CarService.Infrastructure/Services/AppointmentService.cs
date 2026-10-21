using CarService.Core.Common;
using CarService.Core.Entities;
using CarService.Core.Interfaces;
using CarService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CarService.Infrastructure.Services;

public class AppointmentService : IAppointmentService
{
    private readonly CarServiceDbContext _db;
    private readonly TimeProvider _time;

    // TimeProvider (built into .NET 8+) is an abstraction over "what time is it?".
    // WHY: DateTime.Now can't be controlled in a test. With TimeProvider, production
    // uses the real clock and a test passes a fixed one, so "is this date in the past?"
    // becomes deterministic.
    public AppointmentService(CarServiceDbContext db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    public async Task<IReadOnlyList<ServiceAppointment>> GetAllAsync() =>
        await _db.ServiceAppointments
            .Include(a => a.Vehicle).ThenInclude(v => v!.Customer)   // two levels: appointment -> vehicle -> customer
            .AsNoTracking()
            .OrderBy(a => a.ScheduledAt)
            .ToListAsync();

    public async Task<ServiceAppointment?> GetByIdAsync(int id) =>
        await _db.ServiceAppointments
            .Include(a => a.Vehicle).ThenInclude(v => v!.Customer)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id);

    public async Task<OperationResult> CreateAsync(ServiceAppointment appointment)
    {
        if (!await _db.Vehicles.AnyAsync(v => v.Id == appointment.VehicleId))
            return OperationResult.Fail("That vehicle does not exist.");

        // New bookings can't be in the past. (Edits are allowed to keep an old date.)
        if (appointment.ScheduledAt < _time.GetLocalNow().DateTime)
            return OperationResult.Fail("The appointment cannot be scheduled in the past.");

        appointment.Status = AppointmentStatus.Scheduled;   // never trust the caller for the starting state
        _db.ServiceAppointments.Add(appointment);
        await _db.SaveChangesAsync();
        return OperationResult.Ok();
    }

    public async Task<OperationResult> UpdateAsync(int id, string description, DateTime scheduledAt, decimal? cost)
    {
        var appointment = await _db.ServiceAppointments.FindAsync(id);
        if (appointment is null) return OperationResult.Fail("Appointment not found.");
        if (appointment.IsClosed) return OperationResult.Fail($"A {appointment.Status} appointment can no longer be edited.");

        appointment.Description = description;
        appointment.ScheduledAt = scheduledAt;
        appointment.Cost = cost;
        await _db.SaveChangesAsync();
        return OperationResult.Ok();
    }

    public async Task<OperationResult> ChangeStatusAsync(int id, AppointmentStatus next, decimal? cost)
    {
        var appointment = await _db.ServiceAppointments.FindAsync(id);
        if (appointment is null) return OperationResult.Fail("Appointment not found.");

        // The RULES live in the entity; the service just orchestrates: load -> apply -> save.
        var result = appointment.ChangeStatus(next, cost);
        if (result.Succeeded) await _db.SaveChangesAsync();
        return result;
    }
}
