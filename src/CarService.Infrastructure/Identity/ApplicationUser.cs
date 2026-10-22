using Microsoft.AspNetCore.Identity;

namespace CarService.Infrastructure.Identity;

// WHAT: The login account. IdentityUser already has Id, UserName, Email, PasswordHash (a salted
//       hash, never the password), lockout fields and more. We only add what's specific to us.
// WHY:  It lives in Infrastructure, not Core. A login is a technical concern tied to the Identity
//       framework; Core's Customer (someone who brings a car) is a different thing from a User
//       (staff who log in), so they're deliberately separate classes.
public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = "";
}
