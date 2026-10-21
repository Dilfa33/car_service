using System.Net;
using System.Text;
using CarService.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace CarService.Tests;

// A fake "network": every request the HttpClient makes ends up here instead of the internet.
// This is why the decoder takes an HttpClient in its constructor - we can swap the handler.
public class StubHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;
    public List<Uri> Requests { get; } = new();

    public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Requests.Add(request.RequestUri!);
        return Task.FromResult(_respond(request));
    }
}

public class NhtsaVinDecoderTests
{
    private const string ValidVin = "1HGCM82633A004352";

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static (NhtsaVinDecoder decoder, StubHandler handler) Create(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var handler = new StubHandler(respond);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://vpic.example/") };
        return (new NhtsaVinDecoder(http, NullLogger<NhtsaVinDecoder>.Instance), handler);
    }

    [Fact]
    public async Task Decode_CleanVin_ReturnsTitleCasedData_AndCallsTheRightUrl()
    {
        var (decoder, handler) = Create(_ => Json(
            """{"Results":[{"Make":"HONDA","Model":"Accord","ModelYear":"2003","ErrorCode":"0","ErrorText":"0 - VIN decoded clean."}]}"""));

        var result = await decoder.DecodeAsync(ValidVin);

        Assert.True(result.Succeeded);
        Assert.Equal("Honda", result.Make);
        Assert.Equal("Accord", result.Model);
        Assert.Equal(2003, result.Year);
        Assert.Null(result.Message);
        Assert.Equal($"https://vpic.example/api/vehicles/DecodeVinValues/{ValidVin}?format=json", handler.Requests.Single().ToString());
    }

    [Fact]
    public async Task Decode_BadCheckDigit_StillSucceeds_WithWarning()
    {
        var (decoder, _) = Create(_ => Json(
            """{"Results":[{"Make":"HONDA","Model":"Accord","ModelYear":"2003","ErrorCode":"1","ErrorText":"1 - Check Digit (9th position) does not calculate properly"}]}"""));

        var result = await decoder.DecodeAsync(ValidVin);

        Assert.True(result.Succeeded);
        Assert.Contains("Check Digit", result.Message);
    }

    [Fact]
    public async Task Decode_EuropeanVin_MakeButNoModel_ReturnsPartialResult()
    {
        // Shape of what NHTSA really returns for a European VW (WVW...): make + year, empty model.
        var (decoder, _) = Create(_ => Json(
            """{"Results":[{"Make":"VOLKSWAGEN","Model":"","ModelYear":"2006","ErrorCode":"1,4,14","ErrorText":"1 - Check Digit (9th position) does not calculate properly"}]}"""));

        var result = await decoder.DecodeAsync("WVWZZZ1KZ6W612345");

        Assert.True(result.Succeeded);
        Assert.Equal("Volkswagen", result.Make);
        Assert.Null(result.Model);
        Assert.Equal(2006, result.Year);
        Assert.Contains("enter the model", result.Message);
    }

    [Fact]
    public async Task Decode_NothingFound_Fails_WithReason()
    {
        var (decoder, _) = Create(_ => Json(
            """{"Results":[{"Make":"","Model":"","ModelYear":"","ErrorCode":"6,7","ErrorText":"6 - Incomplete VIN; 7 - Manufacturer not registered"}]}"""));

        var result = await decoder.DecodeAsync(ValidVin);

        Assert.False(result.Succeeded);
        Assert.Contains("Incomplete VIN", result.Message);
    }

    [Fact]
    public async Task Decode_UnregisteredManufacturer_ExplainsTheRealReason_NotTheCheckDigit()
    {
        var (decoder, _) = Create(_ => Json(
            """{"Results":[{"Make":"","Model":"","ModelYear":"","ErrorCode":"1,7","ErrorText":"1 - Check Digit (9th position) does not calculate properly; 7 - Manufacturer is not registered with NHTSA"}]}"""));

        var result = await decoder.DecodeAsync("TMBJJ7NE0K0123456");

        Assert.False(result.Succeeded);
        Assert.Contains("not registered", result.Message);
        Assert.DoesNotContain("Check Digit", result.Message);
    }

    [Fact]
    public async Task Decode_MalformedVin_FailsWithoutCallingTheApi()
    {
        var (decoder, handler) = Create(_ => throw new InvalidOperationException("must not be called"));

        var result = await decoder.DecodeAsync("ZZZ");

        Assert.False(result.Succeeded);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Decode_ServerError_FailsGracefully()
    {
        var (decoder, _) = Create(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var result = await decoder.DecodeAsync(ValidVin);

        Assert.False(result.Succeeded);
        Assert.Contains("unavailable", result.Message);
    }

    [Fact]
    public async Task Decode_NetworkFailure_FailsGracefully()
    {
        var (decoder, _) = Create(_ => throw new HttpRequestException("connection refused"));

        Assert.False((await decoder.DecodeAsync(ValidVin)).Succeeded);
    }

    [Fact]
    public async Task Decode_GarbageResponse_FailsGracefully()
    {
        var (decoder, _) = Create(_ => Json("<html>Bad gateway</html>"));

        Assert.False((await decoder.DecodeAsync(ValidVin)).Succeeded);
    }

    [Fact]
    public async Task Decode_EmptyResults_FailsGracefully()
    {
        var (decoder, _) = Create(_ => Json("""{"Results":[]}"""));

        Assert.False((await decoder.DecodeAsync(ValidVin)).Succeeded);
    }
}
