using CarService.Core.Entities;

namespace CarService.Web.ViewModels;

// WHAT: A screen that shows data from SEVERAL entities at once, in the shape the screen needs.
// WHY:  The view should just display. Totals, sorting and flattening are done here, once,
//       and the view stays free of logic (and easy to read).
public class CustomerDetailsViewModel
{
    public required Customer Customer { get; init; }
    public required IReadOnlyList<VehicleRow> Vehicles { get; init; }
    public required IReadOnlyList<AppointmentRow> History { get; init; }
    public decimal TotalSpent { get; init; }

    public record VehicleRow(Vehicle Vehicle, int OpenAppointments, int TotalAppointments);
    public record AppointmentRow(Vehicle Vehicle, ServiceAppointment Appointment);

    public static CustomerDetailsViewModel From(Customer customer)
    {
        var history = customer.Vehicles
            .SelectMany(v => v.Appointments.Select(a => new AppointmentRow(v, a)))
            .OrderByDescending(r => r.Appointment.ScheduledAt)
            .ToList();

        return new CustomerDetailsViewModel
        {
            Customer = customer,
            Vehicles = customer.Vehicles
                .OrderBy(v => v.Make).ThenBy(v => v.Model)
                .Select(v => new VehicleRow(v, v.Appointments.Count(a => !a.IsClosed), v.Appointments.Count))
                .ToList(),
            History = history,
            // Summed in memory, on purpose: SQLite can't SUM a decimal column in SQL (EF throws),
            // but this customer's history is already loaded and small, so it costs nothing.
            TotalSpent = history
                .Where(r => r.Appointment.Status == AppointmentStatus.Completed)
                .Sum(r => r.Appointment.Cost ?? 0)
        };
    }
}
