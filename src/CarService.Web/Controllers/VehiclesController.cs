using CarService.Core.Entities;
using CarService.Core.Interfaces;
using CarService.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace CarService.Web.Controllers;

// MVC in one paragraph:
//   Request /Vehicles/Create  ->  routing picks VehiclesController.Create
//   -> controller asks a service for data -> passes a model to a View (.cshtml)
//   -> Razor renders HTML -> response.
// The route pattern "{controller}/{action}/{id?}" is set in Program.cs; the class
// name minus "Controller" is the controller part, the method name is the action.
public class VehiclesController : Controller
{
    private readonly IVehicleService _vehicles;

    public VehiclesController(IVehicleService vehicles) => _vehicles = vehicles;

    // GET /Vehicles  -> list page
    public async Task<IActionResult> Index()
    {
        var vehicles = await _vehicles.GetAllAsync();
        return View(vehicles);     // looks for Views/Vehicles/Index.cshtml
    }

    // GET /Vehicles/Create -> show the empty form
    [HttpGet]
    public IActionResult Create() => View(new VehicleFormViewModel());

    // POST /Vehicles/Create -> the form was submitted
    // [ValidateAntiForgeryToken] blocks CSRF: the form includes a hidden token
    // (added automatically by the <form> tag helper) that must match a cookie.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(VehicleFormViewModel form)
    {   
        if (!ModelState.IsValid)           // validation attributes failed
            return View(form);             // redisplay the form with error messages

        await _vehicles.CreateAsync(new Vehicle
        {
            Make = form.Make,
            Model = form.Model,
            Year = form.Year,
            LicensePlate = form.LicensePlate,
            CustomerId = form.CustomerId
        });

        // POST-Redirect-GET: redirect after a successful POST so that refreshing
        // the browser doesn't re-submit the form.
        return RedirectToAction(nameof(Index));
    }
}
