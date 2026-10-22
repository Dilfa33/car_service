using CarService.Core.Common;

namespace CarService.Core.Interfaces;

// WHAT: Staff-account management (list / create / delete), as the app needs it.
// WHY:  ASP.NET Core Identity's UserManager is a big framework class. The controller only needs
//       these three operations, so we expose just those. (Signing in is different: SignInManager
//       is tied to the HTTP request and cookies, so the login controller uses it directly.)
public interface IUserAdminService
{
    Task<IReadOnlyList<UserSummary>> GetAllAsync();
    Task<OperationResult> CreateAsync(string email, string fullName, string password, string role);

    // currentUserId = who is asking. Used to stop people deleting their own account.
    Task<OperationResult> DeleteAsync(string userId, string currentUserId);
}

public record UserSummary(string Id, string Email, string FullName, IReadOnlyList<string> Roles);
