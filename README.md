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

## Done so far
- Vehicles: list, create (customer dropdown, duplicate-plate validation, **VIN lookup** via the NHTSA vPIC API:
  `IVinDecoder` in Core, `NhtsaVinDecoder` typed `HttpClient` in Infrastructure, JSON endpoint `Vehicles/DecodeVin`
  called from the form with `fetch`).
- Customers: full CRUD (delete refused while they own vehicles).
- Appointments: book, edit, status workflow `Scheduled -> InProgress -> Completed` (or `Cancelled`).
  The rules live in `ServiceAppointment.ChangeStatus` (Core) and are unit-tested without a database.
  There is deliberately no delete: appointments are cancelled so history is kept.

## Roadmap (next)
1. Vehicle Edit / Delete.
2. Customer details page with their vehicles and appointment history.
3. Search, filter and paging on the lists.
4. VIN extras: cache lookups (`IMemoryCache`), retry/resilience (`Microsoft.Extensions.Http.Resilience`), unique VIN index.
5. **Authentication** with ASP.NET Core Identity.
6. Logging (`ILogger<T>`), a friendly error page, then a small JSON API controller.
