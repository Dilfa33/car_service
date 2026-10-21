using CarService.Core.Interfaces;
using CarService.Infrastructure.Data;
using CarService.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

// Program.cs is the app's entry point and "composition root": the ONE place where
// we decide which concrete class backs each interface.
var builder = WebApplication.CreateBuilder(args);

// MVC: registers controllers, views, model binding, validation.
builder.Services.AddControllersWithViews();

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

// TimeProvider.System = the real clock. Singleton: it has no per-request state.
builder.Services.AddSingleton(TimeProvider.System);

var app = builder.Build();

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

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    // URL pattern. "/" -> Home/Index; "/Vehicles/Create" -> VehiclesController.Create
    pattern: "{controller=Vehicles}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
