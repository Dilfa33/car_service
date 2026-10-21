using System.ComponentModel.DataAnnotations;

namespace CarService.Web.ViewModels;

// WHAT: A ViewModel is a class shaped exactly like a screen/form, not like the DB.
// WHY:  Never bind forms directly to entities: users could post extra fields
//       ("over-posting", e.g. set Id or CustomerId) and the validation messages
//       belong to the UI, not the domain.
// WHEN: One ViewModel per form/page that differs from the entity.
public class VehicleFormViewModel
{
    // DataAnnotations drive BOTH server-side validation (ModelState.IsValid)
    // and client-side validation in the browser (via the jQuery validation scripts).
    [Required, StringLength(50)]
    public string Make { get; set; } = "";

    [Required, StringLength(50)]
    public string Model { get; set; } = "";

    [Range(1950, 2100)]
    public int Year { get; set; } = DateTime.Now.Year;

    [Required, Display(Name = "License plate"), StringLength(15)]
    public string LicensePlate { get; set; } = "";

    [Display(Name = "Owner")]
    public int CustomerId { get; set; }
}
