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

## Logging in (development)
On the first `dotnet run` in Development the app applies migrations and seeds a first admin:

| | |
|---|---|
| Email | `admin@carservice.local` |
| Password | `Admin#12345` (throwaway, defined in `appsettings.Development.json`) |

Log in, open **Staff**, and create accounts for your mechanics. **Never reuse this password.** In production there is no
auto-migration and no seed unless you set `Seed__AdminEmail` and `Seed__AdminPassword` as environment variables;
run `dotnet ef database update` first.

| Role | Can do |
|---|---|
| Mechanic | Vehicles, customers, appointments (everything day-to-day) |
| Admin | All of the above + delete customers + manage staff accounts |

How it's secured: ASP.NET Core Identity (salted password hashes, 5-attempt lockout), an auth cookie (HttpOnly),
a **fallback policy** that makes every page private unless marked `[AllowAnonymous]`, role checks with
`[Authorize(Roles = Roles.Admin)]`, anti-forgery tokens on every POST, logout as POST, and open-redirect protection on login.
`AuthorizationTests` start the real app in memory and prove these rules over HTTP.

## Done so far
- Vehicles: list, create (customer dropdown, duplicate-plate validation, **VIN lookup** via the NHTSA vPIC API:
  `IVinDecoder` in Core, `NhtsaVinDecoder` typed `HttpClient` in Infrastructure, JSON endpoint `Vehicles/DecodeVin`
  called from the form with `fetch`).
- Customers: full CRUD (delete refused while they own vehicles).
- Customer details page (vehicles + full service history + total spent) using `Include`/`ThenInclude`/`AsSplitQuery`.
- Search and paging on Vehicles and Customers, status filter and paging on Appointments (`PagedResult<T>`,
  `ToPagedResultAsync`, shared `_Pager` and `_SearchBox` partials; filters live in the URL).
- Appointments: book, edit, status workflow `Scheduled -> InProgress -> Completed` (or `Cancelled`).
  The rules live in `ServiceAppointment.ChangeStatus` (Core) and are unit-tested without a database.
  There is deliberately no delete: appointments are cancelled so history is kept.

## Roadmap (next)
1. Vehicle Edit / Delete.
2. VIN extras: cache lookups (`IMemoryCache`), retry/resilience (`Microsoft.Extensions.Http.Resilience`), unique VIN index.
3. Authentication extras: change-password page, edit a user's role, HTTPS-only cookies for production.
4. Logging (`ILogger<T>`), a friendly error page, then a small JSON API controller.
