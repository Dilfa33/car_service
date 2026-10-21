using CarService.Core.Entities;

namespace CarService.Core.Interfaces;

// WHAT: An interface is a contract: "anything that calls itself an IVehicleService
//       can do these things". It says WHAT, not HOW.
// WHY:  The controller will depend on this interface, not on the concrete class.
//       That's "Dependency Inversion" - it lets us (a) swap implementations and
//       (b) unit-test controllers with a fake service.
// WHEN: Put an interface in front of anything that does real work or I/O.
//
// Task<T> + async: database calls are I/O. `async` frees the web server thread
// while waiting for the DB, so one server can handle many requests at once.
// Convention: async methods end in "Async".
public interface IVehicleService
{
    Task<IReadOnlyList<Vehicle>> GetAllAsync();
    Task<Vehicle?> GetByIdAsync(int id);
    Task<Vehicle> CreateAsync(Vehicle vehicle);
}
