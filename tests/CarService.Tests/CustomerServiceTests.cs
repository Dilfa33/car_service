using CarService.Core.Entities;
using CarService.Infrastructure.Data;
using CarService.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CarService.Tests;

public class CustomerServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly CarServiceDbContext _db;
    private readonly CustomerService _service;

    public CustomerServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _db = new CarServiceDbContext(
            new DbContextOptionsBuilder<CarServiceDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
        _service = new CustomerService(_db);
    }

    [Fact]
    public async Task UpdateAsync_ChangesFields()
    {
        var created = await _service.CreateAsync(new Customer { FullName = "Old Name" });

        var ok = await _service.UpdateAsync(new Customer { Id = created.Id, FullName = "New Name", Email = "n@x.com" });

        Assert.True(ok);
        var reloaded = await _service.GetByIdAsync(created.Id);
        Assert.Equal("New Name", reloaded!.FullName);
        Assert.Equal("n@x.com", reloaded.Email);
    }

    [Fact]
    public async Task UpdateAsync_UnknownId_ReturnsFalse()
    {
        Assert.False(await _service.UpdateAsync(new Customer { Id = 999, FullName = "Ghost" }));
    }

    [Fact]
    public async Task DeleteAsync_CustomerWithoutVehicles_Deletes()
    {
        var created = await _service.CreateAsync(new Customer { FullName = "Temp" });

        Assert.True(await _service.DeleteAsync(created.Id));
        Assert.Null(await _service.GetByIdAsync(created.Id));
    }

    [Fact]
    public async Task DeleteAsync_CustomerWithVehicles_IsRefused()
    {
        // Seeded customer 1 owns this vehicle.
        _db.Vehicles.Add(new Vehicle { Make = "A", Model = "B", Year = 2020, LicensePlate = "OWN-1", CustomerId = 1 });
        await _db.SaveChangesAsync();

        Assert.False(await _service.DeleteAsync(1));
        Assert.NotNull(await _service.GetByIdAsync(1));   // still there
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
