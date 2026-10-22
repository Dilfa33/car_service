using System.Net;
using System.Text.RegularExpressions;
using CarService.Core.Common;
using CarService.Core.Interfaces;
using CarService.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CarService.Tests;

// WHAT: Starts the REAL application (middleware order, routing, cookies, attributes) inside the test
//       process, with an in-memory database, and talks to it over HTTP.
// WHY:  Security bugs live in the wiring: a missing UseAuthentication(), a wrong attribute, a fallback
//       policy that isn't applied. Unit tests of services can't see those. These tests can.
public class AppFactory : WebApplicationFactory<Program>
{
    // The in-memory SQLite database only lives while this connection stays open.
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public AppFactory() => _connection.Open();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Development => Program.cs applies migrations and seeds admin@carservice.local from
        // appsettings.Development.json, exactly as on a developer's machine.
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            // Swap the real database registration for the in-memory one.
            var toRemove = services.Where(d =>
                d.ServiceType == typeof(DbContextOptions<CarServiceDbContext>) ||
                d.ServiceType == typeof(CarServiceDbContext) ||
                (d.ServiceType.IsGenericType && d.ServiceType.GetGenericArguments().Contains(typeof(CarServiceDbContext))
                    && d.ServiceType.Name.StartsWith("IDbContextOptionsConfiguration"))).ToList();
            foreach (var d in toRemove) services.Remove(d);

            services.AddDbContext<CarServiceDbContext>(o => o.UseSqlite(_connection));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }
}

public class AuthorizationTests : IClassFixture<AppFactory>
{
    private const string AdminEmail = "admin@carservice.local";
    private const string AdminPassword = "Admin#12345";
    private const string MechanicEmail = "mech@carservice.local";
    private const string MechanicPassword = "Mechanic#123";

    private readonly AppFactory _factory;

    public AuthorizationTests(AppFactory factory)
    {
        _factory = factory;
        EnsureMechanic().GetAwaiter().GetResult();
    }

    private async Task EnsureMechanic()
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserAdminService>();
        if ((await users.GetAllAsync()).Any(u => u.Email == MechanicEmail)) return;
        var result = await users.CreateAsync(MechanicEmail, "Mia Mechanic", MechanicPassword, Roles.Mechanic);
        Assert.True(result.Succeeded, result.Error);
    }

    // A client that does NOT follow redirects, so we can assert on the redirect itself.
    // (Cookies are kept between calls automatically, like a browser.)
    private HttpClient NewClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    // The cookie middleware redirects with an ABSOLUTE url (http://localhost/Account/Login?...),
    // while our own LocalRedirect is relative. Compare just the path so both forms work.
    private static string RedirectPath(HttpResponseMessage response)
    {
        var location = response.Headers.Location!;
        return location.IsAbsoluteUri ? location.AbsolutePath : location.OriginalString.Split('?')[0];
    }

    private static async Task<string> AntiForgeryTokenAsync(HttpClient client, string url)
    {
        var html = await client.GetStringAsync(url);
        return Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
    }

    private async Task<HttpClient> LoginAsAsync(string email, string password)
    {
        var client = NewClient();
        var token = await AntiForgeryTokenAsync(client, "/Account/Login");
        var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email, ["Password"] = password, ["__RequestVerificationToken"] = token
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);   // success = redirect away from the form
        return client;
    }

    // ---------- anonymous visitors ----------

    [Theory]
    [InlineData("/")]
    [InlineData("/Vehicles")]
    [InlineData("/Customers")]
    [InlineData("/Customers/Details/1")]
    [InlineData("/Appointments")]
    [InlineData("/Vehicles/DecodeVin?vin=1HGCM82633A004352")]   // the NHTSA proxy must not be public
    [InlineData("/Users")]
    public async Task Anonymous_IsSentToLogin_ForEveryPage(string url)
    {
        var response = await NewClient().GetAsync(url);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/Login", RedirectPath(response));
    }

    [Fact]
    public async Task LoginPage_AndItsStylesheet_ArePublic()
    {
        var client = NewClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Account/Login")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/css/site.css")).StatusCode);
    }

    [Fact]
    public async Task Anonymous_CannotPostToProtectedActions()
    {
        // No antiforgery token either, but the point is the response is a login redirect, not data.
        var response = await NewClient().PostAsync("/Customers/Delete/1", new FormUrlEncodedContent(new Dictionary<string, string>()));

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.NoContent, response.StatusCode);
    }

    // ---------- logging in ----------

    [Fact]
    public async Task Login_WithWrongPassword_ShowsGenericError_AndStaysOnForm()
    {
        var client = NewClient();
        var token = await AntiForgeryTokenAsync(client, "/Account/Login");

        var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = AdminEmail, ["Password"] = "wrong-password", ["__RequestVerificationToken"] = token
        }));
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Invalid email or password.", html);
        Assert.DoesNotContain("wrong-password", html);   // the password is never echoed back
    }

    [Fact]
    public async Task Login_UnknownEmail_GivesTheSameMessageAsWrongPassword()
    {
        var client = NewClient();
        var token = await AntiForgeryTokenAsync(client, "/Account/Login");

        var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = "nobody@nowhere.test", ["Password"] = "Whatever#123", ["__RequestVerificationToken"] = token
        }));

        Assert.Contains("Invalid email or password.", await response.Content.ReadAsStringAsync());   // no user enumeration
    }

    [Fact]
    public async Task Login_ReturnUrlToAnotherSite_IsIgnored()
    {
        var client = NewClient();
        var token = await AntiForgeryTokenAsync(client, "/Account/Login");

        var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = AdminEmail, ["Password"] = AdminPassword, ["ReturnUrl"] = "https://evil.example/phish",
            ["__RequestVerificationToken"] = token
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.DoesNotContain("evil.example", response.Headers.Location!.ToString());   // open redirect blocked
    }

    [Fact]
    public async Task Login_PostWithoutAntiForgeryToken_IsRejected()
    {
        var response = await NewClient().PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = AdminEmail, ["Password"] = AdminPassword
        }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);   // CSRF protection
    }

    // ---------- roles ----------

    [Fact]
    public async Task Mechanic_CanUseTheDailyPages()
    {
        var mechanic = await LoginAsAsync(MechanicEmail, MechanicPassword);

        foreach (var url in new[] { "/Vehicles", "/Customers", "/Customers/Details/1", "/Appointments", "/Appointments/Create" })
            Assert.Equal(HttpStatusCode.OK, (await mechanic.GetAsync(url)).StatusCode);
    }

    [Theory]
    [InlineData("/Customers/Delete/1")]
    [InlineData("/Users")]
    [InlineData("/Users/Create")]
    public async Task Mechanic_IsDeniedAdminPages(string url)
    {
        var mechanic = await LoginAsAsync(MechanicEmail, MechanicPassword);

        var response = await mechanic.GetAsync(url);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/AccessDenied", RedirectPath(response));
    }

    [Fact]
    public async Task Mechanic_CannotDeleteACustomer_EvenWithAValidForm()
    {
        // Hiding the button isn't security. Craft the POST by hand, with a genuine token.
        var admin = await LoginAsAsync(AdminEmail, AdminPassword);
        var mechanic = await LoginAsAsync(MechanicEmail, MechanicPassword);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarServiceDbContext>();
            db.Customers.Add(new CarService.Core.Entities.Customer { Id = 500, FullName = "Delete Me" });
            await db.SaveChangesAsync();
        }

        var token = await AntiForgeryTokenAsync(mechanic, "/Customers/Create");   // any page with a form gives a valid token
        var response = await mechanic.PostAsync("/Customers/Delete/500", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/AccessDenied", RedirectPath(response));
        using var check = _factory.Services.CreateScope();
        Assert.NotNull(await check.ServiceProvider.GetRequiredService<CarServiceDbContext>().Customers.FindAsync(500));   // still there
    }

    [Fact]
    public async Task Admin_CanSeeAdminPages()
    {
        var admin = await LoginAsAsync(AdminEmail, AdminPassword);

        foreach (var url in new[] { "/Users", "/Users/Create", "/Customers/Delete/1" })
            Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task Navbar_ShowsStaffLink_OnlyToAdmins()
    {
        var admin = await LoginAsAsync(AdminEmail, AdminPassword);
        var mechanic = await LoginAsAsync(MechanicEmail, MechanicPassword);

        Assert.Contains("/Users", await admin.GetStringAsync("/Vehicles"));
        Assert.DoesNotContain("/Users", await mechanic.GetStringAsync("/Vehicles"));
    }

    [Fact]
    public async Task Logout_EndsTheSession()
    {
        var client = await LoginAsAsync(MechanicEmail, MechanicPassword);
        var token = await AntiForgeryTokenAsync(client, "/Vehicles/Create");

        await client.PostAsync("/Account/Logout", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }));
        var after = await client.GetAsync("/Vehicles");

        Assert.Equal(HttpStatusCode.Redirect, after.StatusCode);
        Assert.Equal("/Account/Login", RedirectPath(after));
    }

    [Fact]
    public async Task Logout_ViaGet_IsNotAllowed()
    {
        var client = await LoginAsAsync(MechanicEmail, MechanicPassword);

        var response = await client.GetAsync("/Account/Logout");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);   // only POST is routed
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Vehicles")).StatusCode);   // still logged in
    }
}
