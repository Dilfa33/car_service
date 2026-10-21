using CarService.Core.Entities;

namespace CarService.Core.Interfaces;

public interface IVehicleService
{
    Task<IReadOnlyList<Vehicle>> GetAllAsync();
    Task<Vehicle?> GetByIdAsync(int id);
    Task<Vehicle> CreateAsync(Vehicle vehicle);

    // WHY a separate "is it taken?" query: the controller can show a friendly validation
    // message BEFORE trying to insert. The unique index in the DB stays as the safety net.
    Task<bool> IsLicensePlateTakenAsync(string licensePlate);
}
