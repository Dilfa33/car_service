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

    // A controller may depend on several services. Each one is supplied by the DI container.
    public VehiclesController(IVehicleService vehicles, ICustomerService customers)
    {
        _vehicles = vehicles;
        _customers = customers;
    }

    public async Task<IActionResult> Index() => View(await _vehicles.GetAllAsync());

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var form = new VehicleFormViewModel();
        await FillCustomersAsync(form);
        return View(form);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(VehicleFormViewModel form)
    {
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
