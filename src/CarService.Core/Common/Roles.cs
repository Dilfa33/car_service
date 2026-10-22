namespace CarService.Core.Common;

// WHAT: The role names, defined ONCE.
// WHY:  Role names end up in attributes ([Authorize(Roles = "Admin")]), in seeding code and in views.
//       A typo like "Admn" would silently lock everybody out of a page. A constant turns that typo
//       into a compile error. (Attribute arguments must be compile-time constants, hence `const`.)
public static class Roles
{
    public const string Admin = "Admin";         // everything, incl. deleting customers and managing staff
    public const string Mechanic = "Mechanic";   // day-to-day work: customers, vehicles, appointments

    public static readonly IReadOnlyList<string> All = [Admin, Mechanic];
}
