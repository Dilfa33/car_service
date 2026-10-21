using CarService.Core.Entities;
using CarService.Core.Interfaces;
using CarService.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CarService.Web.Controllers;

public class AppointmentsController : Controller
{
    private readonly IAppointmentService _appointments;
    private readonly IVehicleService _vehicles;

    public AppointmentsController(IAppointmentService appointments, IVehicleService vehicles)
    {
        _appointments = appointments;
        _vehicles = vehicles;
    }

    public async Task<IActionResult> Index() => View(await _appointments.GetAllAsync());

    public async Task<IActionResult> Details(int id)
    {
        var appointment = await _appointments.GetByIdAsync(id);
        return appointment is null ? NotFound() : View(appointment);
    }

    // GET /Appointments/Create?vehicleId=3   (the "Book" link on the vehicles list uses this)
    // `int? vehicleId` is bound from the query string; null when absent.
    [HttpGet]
    public async Task<IActionResult> Create(int? vehicleId)
    {
        var form = new AppointmentFormViewModel
        {
            VehicleId = vehicleId ?? 0,
            ScheduledAt = NextFullHour()
        };
        await FillVehiclesAsync(form);
        return View(form);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AppointmentFormViewModel form)
    {
        if (ModelState.IsValid)
        {
            var result = await _appointments.CreateAsync(new ServiceAppointment
            {
                VehicleId = form.VehicleId,
                Description = form.Description,
                ScheduledAt = form.ScheduledAt,
                Cost = form.Cost
            });
            if (result.Succeeded)
            {
                TempData["Success"] = "Appointment booked.";
                return RedirectToAction(nameof(Index));
            }
            // Rule broken in the service: show it as a form-level error (empty key = not tied to a field).
            ModelState.AddModelError("", result.Error!);
        }

        await FillVehiclesAsync(form);
        return View(form);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var a = await _appointments.GetByIdAsync(id);
        if (a is null) return NotFound();
        if (a.IsClosed)
        {
            TempData["Error"] = $"A {a.Status} appointment can no longer be edited.";
            return RedirectToAction(nameof(Details), new { id });
        }

        return View(new AppointmentFormViewModel
        {
            Id = a.Id,
            VehicleId = a.VehicleId,
            VehicleLabel = VehicleLabel(a.Vehicle!),
            Description = a.Description,
            ScheduledAt = a.ScheduledAt,
            Cost = a.Cost
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, AppointmentFormViewModel form)
    {
        if (id != form.Id) return BadRequest();

        // VehicleId isn't editable, so it isn't a field on the Edit form. Without this line its
        // [Range] error would block every save. Removing a key from ModelState drops its error.
        ModelState.Remove(nameof(form.VehicleId));

        if (ModelState.IsValid)
        {
            var result = await _appointments.UpdateAsync(id, form.Description, form.ScheduledAt, form.Cost);
            if (result.Succeeded)
            {
                TempData["Success"] = "Appointment updated.";
                return RedirectToAction(nameof(Details), new { id });
            }
            ModelState.AddModelError("", result.Error!);
        }

        var a = await _appointments.GetByIdAsync(id);
        form.VehicleLabel = a?.Vehicle is null ? null : VehicleLabel(a.Vehicle);
        return View(form);
    }

    // POST /Appointments/ChangeStatus/5 - the form on the Details page.
    // The enum arrives as text ("Completed") and model binding converts it back to the enum.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeStatus(int id, AppointmentStatus newStatus, decimal? cost)
    {
        var result = await _appointments.ChangeStatusAsync(id, newStatus, cost);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded
            ? $"Status changed to {newStatus}."
            : result.Error;
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task FillVehiclesAsync(AppointmentFormViewModel form) =>
        form.Vehicles = (await _vehicles.GetAllAsync())
            .Select(v => new SelectListItem(VehicleLabel(v), v.Id.ToString()))
            .ToList();

    private static string VehicleLabel(Vehicle v) =>
        $"{v.Make} {v.Model} ({v.LicensePlate})" + (v.Customer is null ? "" : $" - {v.Customer.FullName}");

    private static DateTime NextFullHour()
    {
        var now = DateTime.Now;
        return new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0).AddHours(1);
    }
}
