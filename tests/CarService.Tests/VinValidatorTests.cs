using CarService.Core.Common;

namespace CarService.Tests;

public class VinValidatorTests
{
    [Theory]
    [InlineData("1HGCM82633A004352")]
    [InlineData("  1hgcm82633a004352 ")]   // trimmed + case-insensitive
    public void IsWellFormed_ValidVins(string vin) => Assert.True(VinValidator.IsWellFormed(vin));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ZZZ")]                         // too short
    [InlineData("1HGCM82633A0043521")]          // too long
    [InlineData("1HGCM82633A00435I")]           // contains I
    [InlineData("1HGCM82633A00435O")]           // contains O
    [InlineData("1HGCM82633A00435Q")]           // contains Q
    [InlineData("1HGCM82633A0043/2")]           // punctuation
    public void IsWellFormed_RejectsBadInput(string? vin) => Assert.False(VinValidator.IsWellFormed(vin));

    [Fact]
    public void Normalize_TrimsAndUpperCases() => Assert.Equal("ABC", VinValidator.Normalize("  abc "));
}
