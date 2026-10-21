namespace CarService.Core.Entities;

// WHAT: An entity is a plain C# class that represents a row in a database table.
// WHY:  Core holds the "domain" - the things the business is about. It must NOT
//       depend on EF Core, ASP.NET, or anything else. That keeps it testable and
//       lets you swap the database/web layer later without touching it.
// WHEN: Add a new entity whenever the business has a new "thing" with identity.
public class Customer
{
    // EF Core convention: a property called Id (or CustomerId) becomes the primary key.
    public int Id { get; set; }

    // `required` (C# 11) forces callers to set it when creating the object, so you
    // can't accidentally create a Customer without a name. Works together with
    // <Nullable>enable</Nullable>: string = never null, string? = may be null.
    public required string FullName { get; set; }

    public string? Email { get; set; }
    public string? Phone { get; set; }

    // Navigation property: "one customer has many vehicles".
    // EF Core sees this and creates the foreign key on the Vehicle table for you.
    public List<Vehicle> Vehicles { get; set; } = new();
}
