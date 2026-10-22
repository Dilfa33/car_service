using CarService.Core.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CarService.Infrastructure.Identity;

// WHAT: Runs at startup. Makes sure the roles exist, and that there is a first Admin.
// WHY:  No public registration means a brand-new database has NOBODY who can log in. Somebody has to
//       be created out-of-band. We read the first admin's email and password from configuration:
//         - in development they come from appsettings.Development.json (a throwaway local password);
//         - in production, set Seed__AdminEmail / Seed__AdminPassword as environment variables
//           (never commit real credentials). If they're absent, nothing is seeded.
// It is IDEMPOTENT: running it on every startup is safe, it only creates what's missing.
public static class IdentitySeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var config = services.GetRequiredService<IConfiguration>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(IdentitySeeder));

        foreach (var role in Roles.All)
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));

        // Only seed into an EMPTY user table. Once real users exist, config must not create more.
        if (userManager.Users.Any()) return;

        var email = config["Seed:AdminEmail"];
        var password = config["Seed:AdminPassword"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("No users exist and Seed:AdminEmail / Seed:AdminPassword are not configured: nobody will be able to log in.");
            return;
        }

        var admin = new ApplicationUser { UserName = email, Email = email, FullName = "Administrator", EmailConfirmed = true };
        var created = await userManager.CreateAsync(admin, password);
        if (!created.Succeeded)
        {
            // Log the reasons, never the password.
            logger.LogError("Could not seed the admin user: {Errors}", string.Join("; ", created.Errors.Select(e => e.Description)));
            return;
        }
        await userManager.AddToRoleAsync(admin, Roles.Admin);
        logger.LogInformation("Seeded first admin {Email}.", email);
    }
}
