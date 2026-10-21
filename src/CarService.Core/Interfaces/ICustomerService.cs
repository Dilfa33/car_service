using CarService.Core.Entities;

namespace CarService.Core.Interfaces;

public interface ICustomerService
{
    Task<IReadOnlyList<Customer>> GetAllAsync();
    Task<Customer?> GetByIdAsync(int id);
    Task<Customer> CreateAsync(Customer customer);

    // Returns false when the customer doesn't exist, so the controller can answer 404.
    Task<bool> UpdateAsync(Customer customer);

    // Returns false when the customer can't be deleted (not found, or still owns vehicles).
    // A business rule like this belongs in the service, not the controller.
    Task<bool> DeleteAsync(int id);
}
