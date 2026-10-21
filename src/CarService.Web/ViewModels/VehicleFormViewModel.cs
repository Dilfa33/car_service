using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CarService.Web.ViewModels;

public class VehicleFormViewModel
{
    [Required, StringLength(50)]
    public string Make { get; set; } = "";

    [Required, StringLength(50)]
    public string Model { get; set; } = "";

    [Range(1950, 2100)]
    public int Year { get; set; } = DateTime.Now.Year;

    [Required, Display(Name = "License plate"), StringLength(15)]
    public string LicensePlate { get; set; } = "";

    // The dropdown posts the selected customer's Id. The placeholder option has value 0,
    // so Range(1, ...) is what turns "nothing selected" into a validation error.
    [Range(1, int.MaxValue, ErrorMessage = "Please select an owner."), Display(Name = "Owner")]
    public int CustomerId { get; set; }

    // The CHOICES for the dropdown are data for the view, not input from the user.
    // [BindNever] = never fill this from the request; [ValidateNever] = don't validate it.
    // Without ValidateNever, nullable-aware MVC treats a non-nullable property as implicitly
    // [Required] and the form would be invalid on every post.
    // Because it isn't posted back, the controller must re-fill it whenever it redisplays the form.
    [BindNever, ValidateNever]
    public IEnumerable<SelectListItem> Customers { get; set; } = [];
}
