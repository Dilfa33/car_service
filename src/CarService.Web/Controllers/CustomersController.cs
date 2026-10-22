using CarService.Core.Common;
using CarService.Core.Entities;
using CarService.Core.Interfaces;
using CarService.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarService.Web.Controllers;

public class CustomersController : Controller
{
    private readonly ICustomerService _customers;

    public CustomersController(ICustomerService customers) => _customers = customers;

    private const int PageSize = 10;

    // GET /Customers?search=...&page=...
    public async Task<IActionResult> Index(string? search, int page = 1)
    {
        var result = await _customers.SearchAsync(search, page, PageSize);
        return View(new ListViewModel<Customer> { Result = result, Search = search });
    }

    // GET /Customers/Details/5 - everything about one customer on one page.
    public async Task<IActionResult> Details(int id)
    {
        var customer = await _customers.GetWithHistoryAsync(id);
        return customer is null ? NotFound() : View(CustomerDetailsViewModel.From(customer));
    }

    // GET /Customers/Create
    [HttpGet]
    public IActionResult Create() => View(new CustomerFormViewModel());

    // POST /Customers/Create
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CustomerFormViewModel form)
    {
        if (!ModelState.IsValid) return View(form);

        await _customers.CreateAsync(new Customer
        {
            FullName = form.FullName,
            Email = form.Email,
            Phone = form.Phone
        });
        return RedirectToAction(nameof(Index));
    }

    // GET /Customers/Edit/5   ("5" is the {id?} segment from the route pattern)
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var customer = await _customers.GetByIdAsync(id);
        if (customer is null) return NotFound();     // HTTP 404

        return View(new CustomerFormViewModel
        {
            Id = customer.Id,
            FullName = customer.FullName,
            Email = customer.Email,
            Phone = customer.Phone
        });
    }

    // POST /Customers/Edit/5
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, CustomerFormViewModel form)
    {
        // The id in the URL and the Id in the form must agree; a mismatch means a tampered request.
        if (id != form.Id) return BadRequest();
        if (!ModelState.IsValid) return View(form);

        var updated = await _customers.UpdateAsync(new Customer
        {
            Id = form.Id,
            FullName = form.FullName,
            Email = form.Email,
            Phone = form.Phone
        });
        return updated ? RedirectToAction(nameof(Index)) : NotFound();
    }

    // GET /Customers/Delete/5 -> confirmation page.
    // WHY a confirmation page and not a plain link that deletes: GET requests must be
    // safe (no side effects) - browsers prefetch links and crawlers follow them.
    // Destructive actions are always POST.
    // ROLE CHECK: the fallback policy already demands a login for the whole controller; this attribute
    // adds "...and be an Admin" for just these two actions. Mechanics get the AccessDenied page.
    // Put it on the POST too: hiding the button is not security, anyone can craft a request.
    [HttpGet, Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(int id)
    {
        var customer = await _customers.GetByIdAsync(id);
        return customer is null ? NotFound() : View(customer);
    }

    // POST /Customers/Delete/5
    // ActionName("Delete") lets this method answer to /Customers/Delete while having a
    // different C# name - two methods can't share name + parameter list.
    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken, Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        if (!await _customers.DeleteAsync(id))
        {
            // TempData survives exactly one redirect - the standard way to show a flash message.
            TempData["Error"] = "Customer was not deleted. They may still own vehicles.";
        }
        return RedirectToAction(nameof(Index));
    }
}
