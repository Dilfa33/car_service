using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using CarService.Core.Common;
using CarService.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace CarService.Infrastructure.Services;

// WHAT: Decodes VINs with NHTSA's free vPIC API (US government, no API key).
//       Docs: https://vpic.nhtsa.dot.gov/api/
// WHY:  All knowledge of that API - URL shape, JSON shape, error codes - is confined to this ONE
//       class. If NHTSA changes, this is the only file that needs editing.
public class NhtsaVinDecoder : IVinDecoder
{
    private readonly HttpClient _http;
    private readonly ILogger<NhtsaVinDecoder> _logger;

    // TYPED CLIENT: the HttpClient is handed in by the DI container (see AddHttpClient in
    // Program.cs), already configured with base address and timeout. Never write `new HttpClient()`
    // in app code: it leaks sockets and ignores DNS changes. IHttpClientFactory manages the
    // underlying connections for us.
    public NhtsaVinDecoder(HttpClient http, ILogger<NhtsaVinDecoder> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<VinDecodeResult> DecodeAsync(string vin, CancellationToken cancellationToken = default)
    {
        vin = VinValidator.Normalize(vin);
        if (!VinValidator.IsWellFormed(vin))
            return VinDecodeResult.Fail("A VIN has 17 letters and digits, and never uses I, O or Q.");

        try
        {
            // DecodeVinValues returns ONE flat object (Make, Model, ...) - easier than DecodeVin,
            // which returns a long list of {Variable, Value} pairs.
            var response = await _http.GetFromJsonAsync<NhtsaResponse>(
                $"api/vehicles/DecodeVinValues/{Uri.EscapeDataString(vin)}?format=json", cancellationToken);

            var r = response?.Results?.FirstOrDefault();
            if (r is null) return VinDecodeResult.Fail("The VIN service returned an unexpected answer.");

            // NHTSA reports problems in ErrorCode ("0" = clean) and still fills what it can.
            // No make = it does not know this VIN at all (e.g. a manufacturer not sold in the US).
            if (string.IsNullOrWhiteSpace(r.Make))
                return VinDecodeResult.Fail($"No vehicle found for this VIN. ({RootCause(r.ErrorText)})");

            var year = int.TryParse(r.ModelYear, out var y) ? y : (int?)null;

            // PARTIAL RESULT: NHTSA's data is US-market. For most European cars (WVW..., WVG...)
            // it recognises the manufacturer and the year code but has no model. That is still
            // useful, so we return what we have and say plainly what is missing, rather than
            // throwing it away. The year is also less trustworthy outside the US, hence "check it".
            if (string.IsNullOrWhiteSpace(r.Model))
                return new VinDecodeResult(
                    Succeeded: true,
                    Make: TitleCase(r.Make),
                    Model: null,
                    Year: year,
                    Message: "Found the make and year only. NHTSA has no model data for this VIN " +
                             "(common for European cars) - please enter the model and check the year.");

            return new VinDecodeResult(
                Succeeded: true,
                Make: TitleCase(r.Make),
                Model: r.Model.Trim(),
                Year: year,
                Message: r.ErrorCode == "0" ? null : $"Decoded with a warning: {FirstProblem(r.ErrorText)}");
        }
        // EXTERNAL SERVICES FAIL. Network down, DNS, 500s, a timeout, a changed JSON shape...
        // None of these should crash our page. We log the technical detail for developers and
        // give the user a calm message, and they can still type the details by hand.
        catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException
                                   || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            // (TaskCanceledException with OUR token NOT cancelled = HttpClient.Timeout fired.
            //  If the caller cancelled, we let that exception propagate - nobody is waiting.)
            _logger.LogWarning(ex, "VIN lookup for {Vin} failed", vin);
            return VinDecodeResult.Fail("The VIN lookup service is unavailable right now. You can enter the details manually.");
        }
    }

    // ErrorText looks like "1 - Check Digit (9th position) does not calculate properly; 6 - Incomplete VIN".
    private static string FirstProblem(string? errorText) =>
        errorText?.Split(';')[0].Trim() ?? "unknown problem";

    // When nothing was decoded, the check-digit complaint (code 1) is just noise - European VINs do not
    // even have a check digit - so skip it and report the first problem that explains the failure.
    private static string RootCause(string? errorText)
    {
        var problems = errorText?.Split(';').Select(p => p.Trim()).ToList() ?? new();
        return problems.FirstOrDefault(p => !p.StartsWith("1 -")) ?? problems.FirstOrDefault() ?? "unknown problem";
    }

    // NHTSA sends "HONDA"; we want "Honda".
    private static string TitleCase(string s) =>
        CultureInfo.InvariantCulture.TextInfo.ToTitleCase(s.Trim().ToLowerInvariant());

    // Private DTOs: they mirror NHTSA's JSON and nothing else. Only the fields we use are declared;
    // System.Text.Json ignores the other ~150. They are private so the API's shape never leaks out.
    private sealed record NhtsaResponse(List<NhtsaResult>? Results);
    private sealed record NhtsaResult(string? Make, string? Model, string? ModelYear, string? ErrorCode, string? ErrorText);
}
