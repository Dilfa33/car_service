using CarService.Core.Common;
using CarService.Core.Entities;
using CarService.Core.Interfaces;
using CarService.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CarService.Web.Controllers;

public class VehiclesController : Controller
{
    private readonly IVehicleService _vehicles;
    private readonly ICustomerService _customers;
    private readonly IVinDecoder _vinDecoder;

    // A controller may depend on several services. Each one is supplied by the DI container.
    public VehiclesController(IVehicleService vehicles, ICustomerService customers, IVinDecoder vinDecoder)
    {
        _vehicles = vehicles;
        _customers = customers;
        _vinDecoder = vinDecoder;
    }

    public async Task<IActionResult> Index() => View(await _vehicles.GetAllAsync());

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var form = new VehicleFormViewModel();
        await FillCustomersAsync(form);
        return View(form);
    }

    // GET /Vehicles/DecodeVin?vin=...   called by JavaScript on the Create form.
    // WHAT: this action returns JSON (data), not a View (HTML). Same controller, same routing,
    //       different result type. A browser tab and a script both just make HTTP requests.
    // WHY GET: looking something up has no side effects, so no anti-forgery token is needed.
    public async Task<IActionResult> DecodeVin(string? vin, CancellationToken cancellationToken)
    {
        var result = await _vinDecoder.DecodeAsync(vin ?? "", cancellationToken);
        return Json(new { succeeded = result.Succeeded, make = result.Make, model = result.Model, year = result.Year, message = result.Message });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(VehicleFormViewModel form)
    {
        // The JS lookup is a convenience; the server still validates what was actually posted.
        if (!string.IsNullOrWhiteSpace(form.Vin) && !VinValidator.IsWellFormed(form.Vin))
            ModelState.AddModelError(nameof(form.Vin), "A VIN has 17 letters and digits, and never uses I, O or Q.");

        // Business check: ModelState.AddModelError attaches a message to a field, and the
        // asp-validation-for span next to that input displays it - same as an attribute error.
        if (await _vehicles.IsLicensePlateTakenAsync(form.LicensePlate))
            ModelState.AddModelError(nameof(form.LicensePlate), "A vehicle with this license plate already exists.");

        if (!ModelState.IsValid)
        {
            await FillCustomersAsync(form);   // the dropdown choices were not posted back
            return View(form);
        }

        try
        {
            await _vehicles.CreateAsync(new Vehicle
            {
                Make = form.Make,
                Model = form.Model,
                Year = form.Year,
                LicensePlate = form.LicensePlate,
                Vin = form.Vin,
                CustomerId = form.CustomerId
            });
        }
        catch (DbUpdateException)
        {
            // RACE CONDITION safety net: two users can both pass the check above, then both
            // insert. The unique index lets only one win; the loser lands here.
            ModelState.AddModelError(nameof(form.LicensePlate), "A vehicle with this license plate already exists.");
            await FillCustomersAsync(form);
            return View(form);
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task FillCustomersAsync(VehicleFormViewModel form) =>
        form.Customers = (await _customers.GetAllAsync())
            .Select(c => new SelectListItem(c.FullName, c.Id.ToString()))
            .ToList();
}
