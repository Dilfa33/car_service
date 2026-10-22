using CarService.Infrastructure.Identity;
using CarService.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CarService.Web.Controllers;

// [AllowAnonymous] punches a hole in the "everyone must be logged in" fallback policy, for this
// controller only. Without it, nobody could ever reach the login page.
[AllowAnonymous]
public class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ILogger<AccountController> _logger;

    public AccountController(SignInManager<ApplicationUser> signInManager, ILogger<AccountController> logger)
    {
        _signInManager = signInManager;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        // Already signed in? No need to show the form.
        if (User.Identity?.IsAuthenticated == true) return RedirectToLocal(returnUrl);
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel form)
    {
        if (!ModelState.IsValid) return View(form);

        // The email is the UserName (see UserAdminService.CreateAsync).
        // lockoutOnFailure: true => failed attempts are counted and the account locks after 5.
        var result = await _signInManager.PasswordSignInAsync(
            form.Email, form.Password, form.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            _logger.LogInformation("User {Email} logged in.", form.Email);
            return RedirectToLocal(form.ReturnUrl);
        }

        if (result.IsLockedOut)
        {
            _logger.LogWarning("User {Email} is locked out.", form.Email);
            ModelState.AddModelError("", "Too many failed attempts. Try again in 10 minutes.");
        }
        else
        {
            // SAME message whether the email or the password was wrong. Different messages would let
            // an attacker discover which emails have accounts ("user enumeration").
            ModelState.AddModelError("", "Invalid email or password.");
        }

        form.Password = "";   // never send the password back into the page
        return View(form);
    }

    // Logout is a POST: a GET /Logout could be triggered by any <img src="/Account/Logout"> on any
    // website, silently logging users out (a nuisance attack called logout CSRF).
    [HttpPost, ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    public IActionResult AccessDenied() => View();

    // OPEN REDIRECT protection: returnUrl comes from the query string, so an attacker could craft
    // /Account/Login?returnUrl=https://evil.example and bounce a freshly logged-in user to a
    // look-alike site. Only ever redirect to a path on THIS site.
    private IActionResult RedirectToLocal(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : RedirectToAction("Index", "Vehicles");
}
