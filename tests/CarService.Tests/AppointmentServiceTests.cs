using CarService.Core.Entities;
using CarService.Infrastructure.Data;
using CarService.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CarService.Tests;

// A clock we control. "Now" is always 2030-01-15 10:00 UTC, so tests never depend on today's date.
public class FixedTimeProvider : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => new(2030, 1, 15, 10, 0, 0, TimeSpan.Zero);
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}

public class AppointmentServiceTests : IDisposable
{
    private static readonly DateTime Tomorrow = new(2030, 1, 16, 9, 0, 0);
    private static readonly DateTime Yesterday = new(2030, 1, 14, 9, 0, 0);

    private readonly SqliteConnection _connection;
    private readonly CarServiceDbContext _db;
    private readonly AppointmentService _service;
    private readonly int _vehicleId;

    public AppointmentServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _db = new CarServiceDbContext(
            new DbContextOptionsBuilder<CarServiceDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();

        var vehicle = new Vehicle { Make = "Toyota", Model = "Corolla", Year = 2018, LicensePlate = "T-1", CustomerId = 1 };
        _db.Vehicles.Add(vehicle);
        _db.SaveChanges();
        _vehicleId = vehicle.Id;

        _service = new AppointmentService(_db, new FixedTimeProvider());
    }

    private ServiceAppointment Booking(DateTime when, int? vehicleId = null) =>
        new() { Description = "Oil change", ScheduledAt = when, VehicleId = vehicleId ?? _vehicleId };

    [Fact]
    public async Task CreateAsync_ValidBooking_IsSavedAsScheduled()
    {
        var result = await _service.CreateAsync(Booking(Tomorrow));

        Assert.True(result.Succeeded);
        var all = await _service.GetAllAsync();
        Assert.Equal(AppointmentStatus.Scheduled, Assert.Single(all).Status);
        Assert.Equal("Toyota", all[0].Vehicle!.Make);   // Include() loaded the vehicle
    }

    [Fact]
    public async Task CreateAsync_InThePast_IsRejected()
    {
        var result = await _service.CreateAsync(Booking(Yesterday));

        Assert.False(result.Succeeded);
        Assert.Empty(await _service.GetAllAsync());
    }

    [Fact]
    public async Task CreateAsync_UnknownVehicle_IsRejected()
    {
        var result = await _service.CreateAsync(Booking(Tomorrow, vehicleId: 999));

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task ChangeStatusAsync_FullWorkflow_PersistsEachStep()
    {
        await _service.CreateAsync(Booking(Tomorrow));
        var id = (await _service.GetAllAsync())[0].Id;

        Assert.True((await _service.ChangeStatusAsync(id, AppointmentStatus.InProgress, null)).Succeeded);
        Assert.False((await _service.ChangeStatusAsync(id, AppointmentStatus.Completed, null)).Succeeded);   // no cost
        Assert.True((await _service.ChangeStatusAsync(id, AppointmentStatus.Completed, 80m)).Succeeded);

        var saved = await _service.GetByIdAsync(id);
        Assert.Equal(AppointmentStatus.Completed, saved!.Status);
        Assert.Equal(80m, saved.Cost);
    }

    [Fact]
    public async Task UpdateAsync_ClosedAppointment_IsRefused()
    {
        await _service.CreateAsync(Booking(Tomorrow));
        var id = (await _service.GetAllAsync())[0].Id;
        await _service.ChangeStatusAsync(id, AppointmentStatus.Cancelled, null);

        var result = await _service.UpdateAsync(id, "Changed", Tomorrow, null);

        Assert.False(result.Succeeded);
        Assert.Equal("Oil change", (await _service.GetByIdAsync(id))!.Description);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
