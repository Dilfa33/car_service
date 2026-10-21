using CarService.Core.Common;
using CarService.Core.Entities;

namespace CarService.Core.Interfaces;

// There is deliberately no DeleteAsync. A real shop must keep its history (invoices,
// disputes), so appointments are CANCELLED, never deleted. Designing the interface
// without a method is how you make an action impossible.
public interface IAppointmentService
{
    Task<IReadOnlyList<ServiceAppointment>> GetAllAsync();
    Task<ServiceAppointment?> GetByIdAsync(int id);
    Task<OperationResult> CreateAsync(ServiceAppointment appointment);
    Task<OperationResult> UpdateAsync(int id, string description, DateTime scheduledAt, decimal? cost);
    Task<OperationResult> ChangeStatusAsync(int id, AppointmentStatus next, decimal? cost);
}
