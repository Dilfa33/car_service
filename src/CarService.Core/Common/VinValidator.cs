namespace CarService.Core.Common;

// WHAT: Pure rules about what a VIN looks like. No I/O, so it lives in Core and is trivially testable.
// WHY:  Check the shape BEFORE calling the external API. It saves a network round trip, gives an
//       instant message, and means untrusted text never gets put into a URL we send to NHTSA.
public static class VinValidator
{
    public const int Length = 17;

    // A VIN is 17 characters of digits and capital letters, EXCEPT I, O and Q (too easy to
    // confuse with 1 and 0), so the legal set is every digit and letter minus those three.
    private const string Allowed = "0123456789ABCDEFGHJKLMNPRSTUVWXYZ";

    // Canonical form used everywhere (comparison, storage, API calls).
    public static string Normalize(string? vin) => (vin ?? "").Trim().ToUpperInvariant();

    public static bool IsWellFormed(string? vin)
    {
        var v = Normalize(vin);
        return v.Length == Length && v.All(c => Allowed.Contains(c));
    }
}
