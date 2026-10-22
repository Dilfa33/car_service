using CarService.Core.Common;
using CarService.Core.Entities;
using CarService.Core.Interfaces;
using CarService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CarService.Infrastructure.Services;

public class CustomerService : ICustomerService
{
    private readonly CarServiceDbContext _db;

    public CustomerService(CarServiceDbContext db) => _db = db;

    public async Task<IReadOnlyList<Customer>> GetAllAsync() =>
        await _db.Customers
            .AsNoTracking()
            .OrderBy(c => c.FullName)
            .ToListAsync();

    public async Task<PagedResult<Customer>> SearchAsync(string? search, int page, int pageSize)
    {
        var query = _db.Customers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = $"%{search.Trim()}%";
            query = query.Where(c =>
                EF.Functions.Like(c.FullName, term) ||
                EF.Functions.Like(c.Email, term) ||
                EF.Functions.Like(c.Phone, term));
        }

        return await query.OrderBy(c => c.FullName).ThenBy(c => c.Id).ToPagedResultAsync(page, pageSize);
    }

    public async Task<Customer?> GetWithHistoryAsync(int id) =>
        await _db.Customers
            .Include(c => c.Vehicles).ThenInclude(v => v.Appointments)
            .AsNoTracking()
            // One big JOIN across customer x vehicles x appointments repeats the customer and
            // vehicle columns on every row. AsSplitQuery runs one query per collection instead:
            // more round trips, but no duplicated data. Worth it once graphs get this deep.
            .AsSplitQuery()
            .FirstOrDefaultAsync(c => c.Id == id);

    public async Task<Customer?> GetByIdAsync(int id) =>
        await _db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);

    public async Task<Customer> CreateAsync(Customer customer)
    {
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();
        return customer;
    }

    public async Task<bool> UpdateAsync(Customer customer)
    {
        // Load the tracked entity, copy over ONLY the fields the form can edit, save.
        // EF compares against the snapshot it took on load and emits an UPDATE that
        // touches just the changed columns. (This is the case where we WANT tracking,
        // unlike the read-only list queries.)
        var existing = await _db.Customers.FindAsync(customer.Id);
        if (existing is null) return false;

        existing.FullName = customer.FullName;
        existing.Email = customer.Email;
        existing.Phone = customer.Phone;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var customer = await _db.Customers.FindAsync(id);
        if (customer is null) return false;

        // WHY this check: Vehicle.CustomerId is a required foreign key, so EF's default
        // is CASCADE DELETE - removing a customer would silently delete their vehicles
        // (and those vehicles' appointments). We refuse instead.
        if (await _db.Vehicles.AnyAsync(v => v.CustomerId == id)) return false;

        _db.Customers.Remove(customer);
        await _db.SaveChangesAsync();
        return true;
    }
}
