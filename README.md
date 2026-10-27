# PC Parts Marketplace

Multi-vendor marketplace for computer parts (RAM, GPUs, CPUs, storage): an ASP.NET Core Web API plus an ASP.NET Core MVC frontend, on SQL Server with EF Core (Database-First), Repository + Unit of Work.

> Status: **Phase 1 (Foundation)** complete. See the roadmap below.

## Tech stack

.NET 10 / C# 14 · ASP.NET Core (Controllers + MVC) · SQL Server 2025 · EF Core 10 (Database-First) · FluentValidation · Serilog · OpenAPI + Scalar · xUnit v3 · NSubstitute · Testcontainers · Docker Compose · GitHub Actions

## Solution layout

```
Database/               schema.sql (source of truth for the database), reset.sql
PCM.DomainEntities/     Scaffolded entities + PcPartsContext, audit/soft-delete interceptor, enums
PCM.Repositories/       Generic async repository, specific repositories, Unit of Work
PCM.Infrastructure/     DTOs, validators, manual mappings, services (business logic)
PCM.API/                REST controllers, middleware, ProblemDetails, OpenAPI
PCM.UI/                 MVC frontend, calls the API through typed HttpClients
tests/                  Unit tests (services, validators) and integration tests (real SQL Server)
scripts/scaffold.ps1    Regenerates entities from the database
```

Dependencies point one way: `API -> Infrastructure -> Repositories -> DomainEntities`. The UI references Infrastructure only for the shared DTOs and never touches the database.

## Run locally (Visual Studio / dotnet CLI)

Prerequisites: .NET 10 SDK, SQL Server (any edition, 2022+).

1. Create the database:
   ```powershell
   sqlcmd -S localhost -E -C -i Database\schema.sql
   ```
   (or open `Database/schema.sql` in SSMS and press F5). The script is idempotent; to rebuild from scratch run `Database/reset.sql` first.

2. Store the connection string in user-secrets (never in appsettings):
   ```powershell
   dotnet user-secrets set "ConnectionStrings:PcParts" "Server=localhost;Database=PcPartsMarketplace;Trusted_Connection=True;TrustServerCertificate=True" --project PCM.API
   ```

3. Run `PCM.API` and `PCM.UI` (in Visual Studio: right-click the solution > Configure Startup Projects > Multiple startup projects).

| What | URL |
| --- | --- |
| API docs (Scalar) | https://localhost:7101/scalar |
| Brands endpoint | https://localhost:7101/api/v1/brands?page=1&pageSize=5&sort=name |
| Health check | https://localhost:7101/health |
| UI | https://localhost:7201 |

## Run with Docker (one command)

```bash
cp .env.example .env        # then set SA_PASSWORD
docker compose up --build
```

UI on http://localhost:8081, API docs on http://localhost:8080/scalar. SQL Server is exposed on `localhost,14333`.

## Tests

```bash
dotnet test PcPartsMarketplace.slnx
```

Integration tests start a disposable SQL Server 2025 container (Docker must be running) and build it from the same `schema.sql`.

## Database-First workflow

1. Change `Database/schema.sql`.
2. Apply it (`reset.sql` then `schema.sql` in development).
3. Run `pwsh ./scripts/scaffold.ps1` to regenerate `PCM.DomainEntities/Context/PcPartsContext.cs` and `DBEntities/`.
4. Commit both. CI re-scaffolds from the schema and fails if the committed entities differ.

Generated files are never edited by hand; custom code lives in partial classes (`Context/PcPartsContext.Partial.cs`, `Partials/`).

## Design decisions (so far)

- **Database-First from a versioned script**: the schema is reviewed as SQL (types, constraints, filtered indexes) and the entities are generated from it.
- **Database-level safety nets**: CHECK constraints (e.g. `ReservedStock <= Stock` prevents overselling), filtered unique indexes, rowversion concurrency tokens.
- **Audit and soft delete in one place**: an EF Core `SaveChangesInterceptor` sets `CreatedAt`/`UpdatedAt` and turns deletes into `IsDeleted = 1`; a named global query filter hides deleted rows.
- **Repositories never save**: the Unit of Work commits once per business operation and exposes transactions without leaking EF Core types.
- **Consistent errors**: every error is RFC 9457 ProblemDetails with `traceId` and `correlationId`; validation errors are 400 with field names.
- **No secrets in the repo**: user-secrets locally, environment variables in Docker and CI.

## Roadmap

1. ~~Foundation~~ ✔
2. Auth: Identity, JWT + refresh tokens, roles, seller approval
3. Catalog: categories, specs, products, search and filters, seed data
4. Offers and inventory
5. Cart and transactional checkout
6. MVC UI
7. Polish: tests, caching, diagrams, screenshots
