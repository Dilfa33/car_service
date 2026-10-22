using CarService.Core.Common;
using CarService.Core.Interfaces;
using CarService.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CarService.Infrastructure.Services;

public class UserAdminService : IUserAdminService
{
    private readonly UserManager<ApplicationUser> _users;

    // UserManager does the heavy lifting: it hashes passwords, enforces the password policy and
    // unique emails, and talks to the Identity tables.
    public UserAdminService(UserManager<ApplicationUser> users) => _users = users;

    public async Task<IReadOnlyList<UserSummary>> GetAllAsync()
    {
        var users = await _users.Users.OrderBy(u => u.Email).ToListAsync();

        // One query per user for their roles. Fine for a garage with a handful of staff; with
        // thousands of users you'd join the role tables in a single query instead.
        var result = new List<UserSummary>();
        foreach (var u in users)
            result.Add(new UserSummary(u.Id, u.Email ?? "", u.FullName, (await _users.GetRolesAsync(u)).ToList()));
        return result;
    }

    public async Task<OperationResult> CreateAsync(string email, string fullName, string password, string role)
    {
        if (!Roles.All.Contains(role))
            return OperationResult.Fail("Unknown role.");

        var user = new ApplicationUser { UserName = email, Email = email, FullName = fullName };

        // The password is hashed inside CreateAsync; we never see or store the plain text.
        // The policy (length, digits...) is configured in Program.cs and checked here.
        var created = await _users.CreateAsync(user, password);
        if (!created.Succeeded) return Fail(created);

        var added = await _users.AddToRoleAsync(user, role);
        if (!added.Succeeded)
        {
            await _users.DeleteAsync(user);   // don't leave a half-created account without a role
            return Fail(added);
        }
        return OperationResult.Ok();
    }

    public async Task<OperationResult> DeleteAsync(string userId, string currentUserId)
    {
        // Without this rule the only admin could delete themselves and lock everyone out for good.
        if (userId == currentUserId)
            return OperationResult.Fail("You cannot delete your own account.");

        var user = await _users.FindByIdAsync(userId);
        if (user is null) return OperationResult.Fail("User not found.");

        var deleted = await _users.DeleteAsync(user);
        return deleted.Succeeded ? OperationResult.Ok() : Fail(deleted);
    }

    // Identity reports ALL problems at once (e.g. "needs a digit", "needs an uppercase letter").
    private static OperationResult Fail(IdentityResult result) =>
        OperationResult.Fail(string.Join(" ", result.Errors.Select(e => e.Description)));
}
