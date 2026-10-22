using System.Security.Claims;
using CarService.Core.Common;
using CarService.Core.Interfaces;
using CarService.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CarService.Web.Controllers;

// Class-level attribute: every action here needs the Admin role.
// Roles are checked against the role CLAIMS stored in the login cookie.
[Authorize(Roles = Roles.Admin)]
public class UsersController : Controller
{
    private readonly IUserAdminService _users;

    public UsersController(IUserAdminService users) => _users = users;

    public async Task<IActionResult> Index() => View(await _users.GetAllAsync());

    [HttpGet]
    public IActionResult Create() => View(WithChoices(new UserCreateViewModel { Role = Roles.Mechanic }));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(UserCreateViewModel form)
    {
        if (ModelState.IsValid)
        {
            var result = await _users.CreateAsync(form.Email, form.FullName, form.Password, form.Role);
            if (result.Succeeded)
            {
                TempData["Success"] = $"Account created for {form.Email}.";
                return RedirectToAction(nameof(Index));
            }
            ModelState.AddModelError("", result.Error!);   // e.g. weak password, duplicate email
        }

        form.Password = form.ConfirmPassword = "";
        return View(WithChoices(form));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        // The id of whoever is logged in right now, from their cookie's claims.
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _users.DeleteAsync(id, currentUserId);

        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "Account deleted." : result.Error;
        return RedirectToAction(nameof(Index));
    }

    private static UserCreateViewModel WithChoices(UserCreateViewModel form)
    {
        form.RoleChoices = Roles.All.Select(r => new SelectListItem(r, r)).ToList();
        return form;
    }
}
