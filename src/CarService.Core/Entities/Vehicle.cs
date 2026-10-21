namespace CarService.Core.Entities;

public class Vehicle
{
    public int Id { get; set; }

    public required string Make { get; set; }   // e.g. Toyota
    public required string Model { get; set; }  // e.g. Corolla
    public int Year { get; set; }

    // Vehicle Identification Number. Optional now; later you can fill it in
    // from a free API (NHTSA vPIC) - see the README roadmap.
    public string? Vin { get; set; }

    public required string LicensePlate { get; set; }

    // Foreign key + navigation. Both together = a proper relationship.
    // The FK property (CustomerId) is what's stored in the DB column;
    // the navigation (Customer) lets you write vehicle.Customer.FullName in C#.
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public List<ServiceAppointment> Appointments { get; set; } = new();
}
