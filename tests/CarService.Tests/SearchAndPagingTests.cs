using CarService.Core.Entities;
using CarService.Infrastructure.Data;
using CarService.Infrastructure.Services;
using CarService.Web.ViewModels;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CarService.Tests;

public class SearchAndPagingTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly CarServiceDbContext _db;

    public SearchAndPagingTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _db = new CarServiceDbContext(
            new DbContextOptionsBuilder<CarServiceDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();   // customer 1 = "Demo Customer"
    }

    private Vehicle AddVehicle(string make, string model, string plate, int customerId = 1, string? vin = null)
    {
        var v = new Vehicle { Make = make, Model = model, Year = 2020, LicensePlate = plate, Vin = vin, CustomerId = customerId };
        _db.Vehicles.Add(v);
        _db.SaveChanges();
        return v;
    }

    // ---------- paging ----------

    [Fact]
    public async Task Paging_SplitsIntoPages_WithCorrectTotals()
    {
        for (var i = 1; i <= 25; i++) AddVehicle("Make", $"M{i:00}", $"P-{i:00}");
        var service = new VehicleService(_db);

        var page1 = await service.SearchAsync(null, 1, 10);
        var page3 = await service.SearchAsync(null, 3, 10);

        Assert.Equal(25, page1.TotalCount);
        Assert.Equal(3, page1.TotalPages);
        Assert.Equal(10, page1.Items.Count);
        Assert.Equal(5, page3.Items.Count);
        Assert.False(page1.HasPrevious);
        Assert.True(page1.HasNext);
        Assert.False(page3.HasNext);
    }

    [Fact]
    public async Task Paging_NeverRepeatsOrSkipsRows_AcrossPages()
    {
        // Same Make and Model on every row: only the Id tie-breaker keeps the order stable.
        for (var i = 1; i <= 25; i++) AddVehicle("Same", "Same", $"P-{i:00}");
        var service = new VehicleService(_db);

        var ids = new List<int>();
        for (var p = 1; p <= 3; p++) ids.AddRange((await service.SearchAsync(null, p, 10)).Items.Select(v => v.Id));

        Assert.Equal(25, ids.Distinct().Count());
    }

    [Theory]
    [InlineData(999, 3)]   // past the end -> last page
    [InlineData(0, 1)]     // zero -> first page
    [InlineData(-5, 1)]    // negative -> first page
    public async Task Paging_OutOfRangePage_IsClamped(int requested, int expected)
    {
        for (var i = 1; i <= 25; i++) AddVehicle("Make", $"M{i:00}", $"P-{i:00}");

        var result = await new VehicleService(_db).SearchAsync(null, requested, 10);

        Assert.Equal(expected, result.Page);
        Assert.NotEmpty(result.Items);
    }

    [Fact]
    public async Task Paging_EmptyTable_HasOneEmptyPage()
    {
        var result = await new VehicleService(_db).SearchAsync(null, 1, 10);

        Assert.Empty(result.Items);
        Assert.Equal(1, result.TotalPages);
    }

    // ---------- vehicle search ----------

    [Fact]
    public async Task VehicleSearch_MatchesPlateMakeModelVinAndOwner_IgnoringCase()
    {
        var anna = new Customer { FullName = "Anna Schmidt" };
        _db.Customers.Add(anna);
        _db.SaveChanges();
        AddVehicle("Volkswagen", "Golf", "B-GOLF-1", customerId: anna.Id, vin: "WVWZZZ1KZ6W612345");
        AddVehicle("Toyota", "Corolla", "T-1");
        var service = new VehicleService(_db);

        Assert.Single((await service.SearchAsync("golf", 1, 10)).Items);          // model, lower-case
        Assert.Single((await service.SearchAsync("b-golf", 1, 10)).Items);        // plate fragment
        Assert.Single((await service.SearchAsync("WVWZZZ", 1, 10)).Items);        // VIN
        Assert.Single((await service.SearchAsync("schmidt", 1, 10)).Items);       // owner name (a JOIN)
        Assert.Single((await service.SearchAsync("toyota", 1, 10)).Items);
        Assert.Empty((await service.SearchAsync("nothing-matches", 1, 10)).Items);
        Assert.Equal(2, (await service.SearchAsync("   ", 1, 10)).TotalCount);    // blank = everything
    }

    [Fact]
    public async Task VehicleSearch_CountReflectsTheFilter_NotTheTable()
    {
        for (var i = 1; i <= 15; i++) AddVehicle("Skoda", "Octavia", $"S-{i:00}");
        for (var i = 1; i <= 5; i++) AddVehicle("Audi", "A4", $"A-{i:00}");

        var result = await new VehicleService(_db).SearchAsync("skoda", 2, 10);

        Assert.Equal(15, result.TotalCount);
        Assert.Equal(5, result.Items.Count);
    }

    // ---------- customer search ----------

    [Fact]
    public async Task CustomerSearch_MatchesNameEmailAndPhone()
    {
        _db.Customers.AddRange(
            new Customer { FullName = "Bob Builder", Email = "bob@build.com", Phone = "555-0101" },
            new Customer { FullName = "Carla Cruz", Email = "carla@cruz.org" });
        _db.SaveChanges();
        var service = new CustomerService(_db);

        Assert.Single((await service.SearchAsync("builder", 1, 10)).Items);
        Assert.Single((await service.SearchAsync("cruz.org", 1, 10)).Items);
        Assert.Single((await service.SearchAsync("0101", 1, 10)).Items);
    }

    // ---------- appointments filter ----------

    [Fact]
    public async Task AppointmentSearch_FiltersByStatus_NewestFirst()
    {
        var v = AddVehicle("A", "B", "X-1");
        _db.ServiceAppointments.AddRange(
            new ServiceAppointment { VehicleId = v.Id, Description = "old", ScheduledAt = new(2030, 1, 1) },
            new ServiceAppointment { VehicleId = v.Id, Description = "new", ScheduledAt = new(2030, 6, 1) },
            new ServiceAppointment { VehicleId = v.Id, Description = "done", ScheduledAt = new(2030, 3, 1), Status = AppointmentStatus.Completed, Cost = 10 });
        _db.SaveChanges();
        var service = new AppointmentService(_db, TimeProvider.System);

        var scheduled = await service.SearchAsync(AppointmentStatus.Scheduled, 1, 10);
        var all = await service.SearchAsync(null, 1, 10);

        Assert.Equal(new[] { "new", "old" }, scheduled.Items.Select(a => a.Description));
        Assert.Equal(3, all.TotalCount);
        Assert.Equal("new", all.Items[0].Description);
    }

    // ---------- customer details ----------

    [Fact]
    public async Task GetWithHistory_LoadsVehiclesAndTheirAppointments()
    {
        var v1 = AddVehicle("Toyota", "Corolla", "H-1");
        var v2 = AddVehicle("Honda", "Civic", "H-2");
        _db.ServiceAppointments.AddRange(
            new ServiceAppointment { VehicleId = v1.Id, Description = "a", ScheduledAt = new(2030, 1, 1) },
            new ServiceAppointment { VehicleId = v1.Id, Description = "b", ScheduledAt = new(2030, 2, 1) },
            new ServiceAppointment { VehicleId = v2.Id, Description = "c", ScheduledAt = new(2030, 3, 1) });
        _db.SaveChanges();

        var customer = await new CustomerService(_db).GetWithHistoryAsync(1);

        Assert.Equal(2, customer!.Vehicles.Count);
        Assert.Equal(3, customer.Vehicles.Sum(v => v.Appointments.Count));
        Assert.Null(await new CustomerService(_db).GetWithHistoryAsync(999));
    }

    [Fact]
    public void DetailsViewModel_SumsOnlyCompletedWork_AndCountsOpenAppointments()
    {
        var vehicle = new Vehicle { Make = "A", Model = "B", Year = 2020, LicensePlate = "Z-1", CustomerId = 1 };
        vehicle.Appointments.AddRange(new[]
        {
            new ServiceAppointment { Description = "paid", Status = AppointmentStatus.Completed, Cost = 100.50m, ScheduledAt = new(2030, 1, 1) },
            new ServiceAppointment { Description = "paid too", Status = AppointmentStatus.Completed, Cost = 50m, ScheduledAt = new(2030, 2, 1) },
            new ServiceAppointment { Description = "cancelled", Status = AppointmentStatus.Cancelled, Cost = 999m, ScheduledAt = new(2030, 3, 1) },
            new ServiceAppointment { Description = "upcoming", ScheduledAt = new(2030, 4, 1) }
        });
        var customer = new Customer { FullName = "T" };
        customer.Vehicles.Add(vehicle);

        var vm = CustomerDetailsViewModel.From(customer);

        Assert.Equal(150.50m, vm.TotalSpent);                  // cancelled 999 is not counted
        Assert.Equal(1, vm.Vehicles.Single().OpenAppointments);
        Assert.Equal(4, vm.History.Count);
        Assert.Equal("upcoming", vm.History[0].Appointment.Description);   // newest first
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
