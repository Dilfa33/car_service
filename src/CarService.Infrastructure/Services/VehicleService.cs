using CarService.Core.Common;
using CarService.Core.Entities;
using CarService.Core.Interfaces;
using CarService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CarService.Infrastructure.Services;

// WHAT: The concrete implementation of the IVehicleService contract.
// WHY:  Controllers should only handle HTTP (read request, return a view). Business
//       rules and data access live in services, so they can be reused and tested.
// NOTE: It sits in Infrastructure because it uses EF Core directly. (A stricter
//       "clean architecture" adds a repository layer; for learning, service+DbContext
//       is simpler and still keeps the web layer clean.)
public class VehicleService : IVehicleService
{
    private readonly CarServiceDbContext _db;

    // CONSTRUCTOR INJECTION: we don't write `new CarServiceDbContext()`. We just
    // ask for it in the constructor and ASP.NET's DI container supplies it.
    public VehicleService(CarServiceDbContext db) => _db = db;

    public async Task<IReadOnlyList<Vehicle>> GetAllAsync() =>
        await _db.Vehicles
            .Include(v => v.Customer)      // JOIN the customer so the view can show the owner
            .AsNoTracking()                // read-only query: faster, EF skips change tracking
            .OrderBy(v => v.Make).ThenBy(v => v.Model)
            .ToListAsync();                // the SQL actually runs HERE (LINQ is lazy until then)

    public async Task<Vehicle?> GetByIdAsync(int id) =>
        await _db.Vehicles.Include(v => v.Customer).FirstOrDefaultAsync(v => v.Id == id);

    public async Task<Vehicle> CreateAsync(Vehicle vehicle)
    {
        vehicle.LicensePlate = NormalizePlate(vehicle.LicensePlate);
        vehicle.Vin = string.IsNullOrWhiteSpace(vehicle.Vin) ? null : VinValidator.Normalize(vehicle.Vin);
        _db.Vehicles.Add(vehicle);         // mark as "to insert"
        await _db.SaveChangesAsync();      // INSERT happens here, Id gets filled in
        return vehicle;
    }

    public async Task<bool> IsLicensePlateTakenAsync(string licensePlate)
    {
        var plate = NormalizePlate(licensePlate);
        return await _db.Vehicles.AnyAsync(v => v.LicensePlate == plate);   // translates to SELECT EXISTS(...)
    }

    // WHY normalize: SQLite compares text case-sensitively, so "abc-123" and "ABC-123"
    // would both pass the unique index. Storing one canonical form (trimmed, upper-case)
    // makes the index and the lookup agree. Do it in ONE place - the service.
    private static string NormalizePlate(string plate) => plate.Trim().ToUpperInvariant();
}
