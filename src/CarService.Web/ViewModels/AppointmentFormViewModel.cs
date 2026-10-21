using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CarService.Web.ViewModels;

// Serves Create and Edit. On Edit the vehicle is fixed (shown as text, not changeable).
public class AppointmentFormViewModel
{
    public int Id { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Please select a vehicle."), Display(Name = "Vehicle")]
    public int VehicleId { get; set; }

    [Required, StringLength(500)]
    public string Description { get; set; } = "";

    // asp-for on a DateTime renders <input type="datetime-local">: the browser gives a
    // native date + time picker for free.
    [Display(Name = "Date and time")]
    public DateTime ScheduledAt { get; set; }

    // decimal? = optional. The cost is usually only known once the work is done.
    [Range(0, 1_000_000)]
    public decimal? Cost { get; set; }

    [BindNever, ValidateNever] public IEnumerable<SelectListItem> Vehicles { get; set; } = [];
    [BindNever, ValidateNever] public string? VehicleLabel { get; set; }   // Edit only
}
