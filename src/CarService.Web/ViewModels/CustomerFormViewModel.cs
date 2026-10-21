using System.ComponentModel.DataAnnotations;

namespace CarService.Web.ViewModels;

// One ViewModel serves both Create and Edit. Id is 0 on Create and set on Edit.
public class CustomerFormViewModel
{
    public int Id { get; set; }

    [Required, StringLength(100), Display(Name = "Full name")]
    public string FullName { get; set; } = "";

    // Optional fields: no [Required]. The format check only runs when a value is present.
    [EmailAddress, StringLength(100)]
    public string? Email { get; set; }

    [Phone, StringLength(30)]
    public string? Phone { get; set; }
}
