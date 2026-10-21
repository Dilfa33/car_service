using CarService.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarService.Infrastructure.Data;

// WHAT: DbContext is EF Core's "session" with the database. Each DbSet<T> is a table.
// WHY:  EF Core is an ORM: you work with C# objects + LINQ, and it generates SQL.
//       It lives in Infrastructure because it's a technical detail (how we store
//       data), not business logic.
// WHEN: One DbContext per app (for now). Add a DbSet for every entity you want
//       to query. Lifetime is "scoped" = one instance per web request.
public class CarServiceDbContext : DbContext
{
    // The options (which DB provider, connection string) are injected from Program.cs.
    public CarServiceDbContext(DbContextOptions<CarServiceDbContext> options) : base(options) { }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<ServiceAppointment> ServiceAppointments => Set<ServiceAppointment>();

    // Fluent API: configuration that conventions can't express.
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Vehicle>()
            .HasIndex(v => v.LicensePlate)
            .IsUnique();                        // DB itself rejects duplicate plates

        modelBuilder.Entity<ServiceAppointment>()
            .Property(a => a.Cost)
            .HasPrecision(10, 2);               // money: up to 99,999,999.99

        // Seed data: inserted by the migration. Gives the form a valid CustomerId
        // (1) until you build the Customers pages.
        modelBuilder.Entity<Customer>().HasData(
            new Customer { Id = 1, FullName = "Demo Customer", Email = "demo@example.com" });
    }
}
