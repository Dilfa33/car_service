using System.Net;
using System.Text.RegularExpressions;
using CarService.Core.Common;
using CarService.Core.Interfaces;
using CarService.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace CarService.Tests;

// Each test class with an IClassFixture<AppFactory> gets its OWN app + empty in-memory database,
// so these tests can't disturb the ones in AuthorizationTests (e.g. by locking the admin account).
public class UserAdminTests : IClassFixture<AppFactory>
{
    private readonly AppFactory _factory;

    public UserAdminTests(AppFactory factory) => _factory = factory;

    private IServiceScope Scope() => _factory.Services.CreateScope();

    [Fact]
    public async Task Create_WeakPassword_IsRejected_AndNoAccountIsLeftBehind()
    {
        using var scope = Scope();
        var users = scope.ServiceProvider.GetRequiredService<IUserAdminService>();

        var result = await users.CreateAsync("weak@test.local", "Weak User", "abc", Roles.Mechanic);

        Assert.False(result.Succeeded);
        Assert.Contains("at least 8", result.Error);
        Assert.DoesNotContain(await users.GetAllAsync(), u => u.Email == "weak@test.local");
    }

    [Fact]
    public async Task Create_DuplicateEmail_IsRejected()
    {
        using var scope = Scope();
        var users = scope.ServiceProvider.GetRequiredService<IUserAdminService>();
        await users.CreateAsync("dup@test.local", "First", "Strong#1234", Roles.Mechanic);

        var second = await users.CreateAsync("dup@test.local", "Second", "Strong#1234", Roles.Mechanic);

        Assert.False(second.Succeeded);
        Assert.Single(await users.GetAllAsync(), u => u.Email == "dup@test.local");
    }

    [Fact]
    public async Task Create_UnknownRole_IsRejected()
    {
        using var scope = Scope();
        var users = scope.ServiceProvider.GetRequiredService<IUserAdminService>();

        var result = await users.CreateAsync("role@test.local", "Role Test", "Strong#1234", "SuperUser");

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Create_StoresAHashNotThePassword_AndAssignsTheRole()
    {
        using var scope = Scope();
        var users = scope.ServiceProvider.GetRequiredService<IUserAdminService>();
        await users.CreateAsync("hash@test.local", "Hash Test", "Strong#1234", Roles.Admin);

        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await manager.FindByEmailAsync("hash@test.local");

        Assert.NotNull(user);
        Assert.DoesNotContain("Strong#1234", user!.PasswordHash);   // a salted hash, never the password
        Assert.True(await manager.IsInRoleAsync(user, Roles.Admin));
    }

    [Fact]
    public async Task Delete_YourOwnAccount_IsRefused_ButAnotherAccountIsFine()
    {
        using var scope = Scope();
        var users = scope.ServiceProvider.GetRequiredService<IUserAdminService>();
        await users.CreateAsync("a@test.local", "A", "Strong#1234", Roles.Admin);
        await users.CreateAsync("b@test.local", "B", "Strong#1234", Roles.Mechanic);
        var all = await users.GetAllAsync();
        var a = all.Single(u => u.Email == "a@test.local");
        var b = all.Single(u => u.Email == "b@test.local");

        Assert.False((await users.DeleteAsync(a.Id, currentUserId: a.Id)).Succeeded);
        Assert.True((await users.DeleteAsync(b.Id, currentUserId: a.Id)).Succeeded);
        Assert.DoesNotContain(await users.GetAllAsync(), u => u.Email == "b@test.local");
    }

    [Fact]
    public async Task Seeder_RunsTwice_WithoutCreatingDuplicates()
    {
        using var scope = Scope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var before = manager.Users.Count(u => u.Email == "admin@carservice.local");

        await IdentitySeeder.SeedAsync(scope.ServiceProvider);   // the app already ran it once at startup
        await IdentitySeeder.SeedAsync(scope.ServiceProvider);

        Assert.Equal(1, before);
        Assert.Equal(1, manager.Users.Count(u => u.Email == "admin@carservice.local"));
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        Assert.True(await roles.RoleExistsAsync(Roles.Admin));
        Assert.True(await roles.RoleExistsAsync(Roles.Mechanic));
    }

    [Fact]
    public async Task Login_AfterFiveWrongPasswords_LocksTheAccount_EvenForTheRightPassword()
    {
        using (var scope = Scope())
            await scope.ServiceProvider.GetRequiredService<IUserAdminService>()
                .CreateAsync("lock@test.local", "Lock Test", "Strong#1234", Roles.Mechanic);

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        async Task<string> TryLogin(string password)
        {
            var html = await client.GetStringAsync("/Account/Login");
            var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
            var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Email"] = "lock@test.local", ["Password"] = password, ["__RequestVerificationToken"] = token
            }));
            return response.StatusCode == HttpStatusCode.Redirect ? "OK" : await response.Content.ReadAsStringAsync();
        }

        for (var i = 0; i < 5; i++) await TryLogin("Wrong#Password1");

        var withCorrectPassword = await TryLogin("Strong#1234");
        Assert.NotEqual("OK", withCorrectPassword);
        Assert.Contains("Too many failed attempts", withCorrectPassword);
    }
}
