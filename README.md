# CarService

A learning project: an ASP.NET Core MVC web app for a car service shop (customers, vehicles, appointments).

## Run it

```bash
cd src/CarService.Web
dotnet ef database update -p ../CarService.Infrastructure -s .   # create/upgrade carservice.db
dotnet run                                                       # then open the URL it prints
dotnet test                                                      # from the repo root
```

## Architecture: what, why, when

```
Web  ──►  Infrastructure  ──►  Core
          (EF Core, services)  (entities, interfaces)
Tests ──► Core + Infrastructure
```

The rule: **dependencies point inward**. Core knows nothing about the others.

| Project | What lives here | Why separate | Add here when... |
|---|---|---|---|
| `CarService.Core` | Entities (`Vehicle`), interfaces (`IVehicleService`) | Pure business concepts; no framework dependency, so easy to test and reuse | you have a new domain concept or a new capability to promise |
| `CarService.Infrastructure` | `DbContext`, migrations, service implementations, (later) API clients | Technical details that may change (DB, external APIs) | you talk to a database, file, or external API |
| `CarService.Web` | Controllers, Views, ViewModels, `Program.cs` | Only HTTP/UI concerns | you add a page, form, or endpoint |
| `CarService.Tests` | xUnit tests | Safety net for refactoring | always, alongside new logic |

### A request, end to end (`GET /Vehicles`)
1. **Routing** (`Program.cs` pattern) maps the URL to `VehiclesController.Index`.
2. DI container builds the controller and injects `IVehicleService` -> `VehicleService` -> `CarServiceDbContext`.
3. The service runs a LINQ query; EF Core translates it to SQL for SQLite.
4. The controller passes the entities to `Views/Vehicles/Index.cshtml`.
5. Razor renders HTML; the layout (`_Layout.cshtml`) wraps it.

### Concepts worth knowing, and where to see them
- **Dependency Injection**: `Program.cs` registrations + constructor parameters.
- **EF Core + migrations**: `DbContext`, `Data/Migrations/`. Change an entity, then run
  `dotnet ef migrations add <Name> -p src/CarService.Infrastructure -s src/CarService.Web -o Data/Migrations`.
- **ViewModels & validation**: `VehicleFormViewModel`.
- **async/await**: every service and action that touches the database.
- **POST-Redirect-GET and anti-forgery tokens**: `VehiclesController.Create`.
- **Testing with real SQLite in memory**: `VehicleServiceTests`.

## Roadmap (your exercises, in order)
1. **Customers CRUD**: new `ICustomerService`, `CustomerService`, `CustomersController`, views. Copy the Vehicles pattern.
2. Replace the `CustomerId` number input with a **dropdown** (`SelectList` in the ViewModel).
3. Vehicle **Details / Edit / Delete** pages.
4. **Appointments**: schedule for a vehicle, change status.
5. **External API**: decode VINs with the free [NHTSA vPIC API](https://vpic.nhtsa.dot.gov/api/) through a typed `HttpClient` (`AddHttpClient<IVinDecoder, NhtsaVinDecoder>()`), in Infrastructure.
6. **Authentication** with ASP.NET Core Identity.
7. **Error handling & logging** (`ILogger<T>`), then a small JSON API controller.
