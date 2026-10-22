using CarService.Core.Interfaces;
using CarService.Infrastructure.Data;
using CarService.Infrastructure.Identity;
using CarService.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

// Program.cs is the app's entry point and "composition root": the ONE place where
// we decide which concrete class backs each interface.
var builder = WebApplication.CreateBuilder(args);

// MVC: registers controllers, views, model binding, validation.
builder.Services.AddControllersWithViews();

// ---- Authentication & authorization -------------------------------------------------------
// AUTHENTICATION = "who are you?" (login).   AUTHORIZATION = "what may you do?" (roles/policies).
//
// AddIdentity wires up: UserManager/SignInManager/RoleManager, password hashing, lockout, and the
// login COOKIE. After a successful login the browser holds a signed, encrypted cookie; on every
// later request the authentication middleware reads it and sets HttpContext.User.
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = false;   // length matters more than symbols

        options.User.RequireUniqueEmail = true;

        // After 5 wrong passwords the account is locked for 10 minutes: slows down password guessing.
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
    })
    // "Store the users and roles in our DbContext" (i.e. in the same SQLite database).
    .AddEntityFrameworkStores<CarServiceDbContext>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";               // where anonymous users are sent
    options.AccessDeniedPath = "/Account/AccessDenied"; // where logged-in-but-not-allowed users are sent
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;                   // activity extends the session
    // HttpOnly (default) hides the cookie from JavaScript, so an XSS bug can't steal the session.
});

// SECURE BY DEFAULT: every endpoint requires a logged-in user unless it says [AllowAnonymous].
// The alternative - putting [Authorize] on each controller - fails open: forget it on ONE new
// controller and that page is public. With a fallback policy, forgetting makes it private.
builder.Services.AddAuthorization(options =>
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

// Database. The connection string lives in appsettings.json so it can differ
// per environment (dev/prod) without code changes.
builder.Services.AddDbContext<CarServiceDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")));

// Dependency injection lifetimes:
//   AddScoped    - one instance per HTTP request  (use for DbContext + services)
//   AddSingleton - one instance for the app's life (caches, config)
//   AddTransient - a new instance every time asked (lightweight stateless helpers)
// "When someone asks for IVehicleService, give them a VehicleService."
builder.Services.AddScoped<IVehicleService, VehicleService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IAppointmentService, AppointmentService>();
builder.Services.AddScoped<IUserAdminService, UserAdminService>();

// Typed HttpClient: "when someone asks for IVinDecoder, build an NhtsaVinDecoder and give it an
// HttpClient configured like this". The base URL comes from configuration, not code.
// The timeout stops a slow external API from hanging our page; the decoder turns it into a friendly message.
builder.Services.AddHttpClient<IVinDecoder, NhtsaVinDecoder>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Nhtsa:BaseUrl"]!);
    client.Timeout = TimeSpan.FromSeconds(10);
});

// TimeProvider.System = the real clock. Singleton: it has no per-request state.
builder.Services.AddSingleton(TimeProvider.System);

var app = builder.Build();

// Startup work: bring the database up to date (development only - in production you run migrations
// deliberately), then make sure roles and the first admin exist. A new scope is needed because
// DbContext and the Identity managers are scoped (one per request) and there is no request here.
using (var scope = app.Services.CreateScope())
{
    if (app.Environment.IsDevelopment())
        scope.ServiceProvider.GetRequiredService<CarServiceDbContext>().Database.Migrate();

    await IdentitySeeder.SeedAsync(scope.ServiceProvider);
}

// The middleware PIPELINE: every request flows through these in order, top to bottom.
// Order matters (e.g. routing before authorization).
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

// ORDER MATTERS: authentication first (read the cookie, set User), THEN authorization (check User).
// Both must come after routing, because the decision depends on which endpoint was matched.
app.UseAuthentication();
app.UseAuthorization();

// The fallback policy covers static files (CSS/JS) too, which would break the login page's styling,
// so they are explicitly public.
app.MapStaticAssets().AllowAnonymous();

app.MapControllerRoute(
    name: "default",
    // URL pattern. "/" -> Home/Index; "/Vehicles/Create" -> VehiclesController.Create
    pattern: "{controller=Vehicles}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();

// Makes the Program class visible to the test project, so tests can start the real app in memory.
public partial class Program;
