using CarService.Core.Entities;
using CarService.Infrastructure.Data;
using CarService.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CarService.Tests;

// WHAT: xUnit test class. Each [Fact] is one test; xUnit makes a NEW instance of
//       this class per test, so the constructor is "setup" and Dispose is "teardown".
// WHY:  An in-memory SQLite database behaves like the real one (unique indexes,
//       foreign keys) - unlike EF's InMemory provider, which ignores them.
// PATTERN: Arrange (set up) -> Act (call the code) -> Assert (check the result).
public class VehicleServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly CarServiceDbContext _db;

    public VehicleServiceTests()
    {
        // An in-memory SQLite DB lives only as long as its connection stays open.
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _db = new CarServiceDbContext(
            new DbContextOptionsBuilder<CarServiceDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();   // build the schema (includes the seeded customer, Id = 1)
    }

    [Fact]
    public async Task CreateAsync_SavesVehicle_AndGeneratesId()
    {
        var service = new VehicleService(_db);

        var created = await service.CreateAsync(new Vehicle
        {
            Make = "Toyota", Model = "Corolla", Year = 2018,
            LicensePlate = "ABC-123", CustomerId = 1
        });

        Assert.True(created.Id > 0);
        Assert.Single(await service.GetAllAsync());
    }

    [Fact]
    public async Task CreateAsync_DuplicatePlate_Throws()
    {
        var service = new VehicleService(_db);
        await service.CreateAsync(new Vehicle { Make = "A", Model = "B", Year = 2020, LicensePlate = "DUP-1", CustomerId = 1 });

        // The unique index from OnModelCreating is enforced by the DB.
        await Assert.ThrowsAsync<DbUpdateException>(() =>
            service.CreateAsync(new Vehicle { Make = "C", Model = "D", Year = 2021, LicensePlate = "DUP-1", CustomerId = 1 }));
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
