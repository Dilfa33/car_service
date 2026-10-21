namespace CarService.Core.Interfaces;

// WHAT: "Given a VIN, tell me what car it is." The contract does not mention HTTP, JSON or NHTSA.
// WHY:  The web layer depends on this interface. Today the implementation calls NHTSA; tomorrow
//       it could use a paid API, a cache, or (in tests) a fake. Nothing else would change.
public interface IVinDecoder
{
    // CancellationToken: ASP.NET cancels it when the browser disconnects, so we stop waiting
    // on a slow external service nobody is listening for any more.
    Task<VinDecodeResult> DecodeAsync(string vin, CancellationToken cancellationToken = default);
}

// Succeeded + data   = decoded (Message may carry a warning, e.g. "check digit looks wrong").
// !Succeeded         = nothing usable; Message explains why, in words fit to show the user.
public record VinDecodeResult(bool Succeeded, string? Make = null, string? Model = null, int? Year = null, string? Message = null)
{
    public static VinDecodeResult Fail(string message) => new(false, Message: message);
}
