# PC Parts Marketplace

Multi-vendor marketplace for computer parts (RAM, GPUs, CPUs, storage): an ASP.NET Core Web API plus an ASP.NET Core MVC frontend, on SQL Server with EF Core (Database-First), Repository + Unit of Work.

> Status: **Phase 2 (Auth)** complete. See the roadmap below.

## Tech stack

.NET 10 / C# 14 · ASP.NET Core (Controllers + MVC) · ASP.NET Core Identity + JWT · SQL Server 2025 · EF Core 10 (Database-First) · FluentValidation · Serilog · OpenAPI + Scalar · xUnit v3 · NSubstitute · Testcontainers · Docker Compose · GitHub Actions

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

3. Optional: a stable JWT signing key (without it, Development generates a random key per run and users are signed out when the API restarts):
   ```powershell
   dotnet user-secrets set "Jwt:SigningKey" "$([guid]::NewGuid().ToString('N'))$([guid]::NewGuid().ToString('N'))" --project PCM.API
   ```

4. Run `PCM.API` and `PCM.UI` (in Visual Studio: right-click the solution > Configure Startup Projects > Multiple startup projects).

| What | URL |
| --- | --- |
| API docs (Scalar) | https://localhost:7101/scalar |
| Brands endpoint | https://localhost:7101/api/v1/brands?page=1&pageSize=5&sort=name |
| Health check | https://localhost:7101/health |
| UI | https://localhost:7201 |

### Demo accounts (Development only)

Created on API startup when the environment is Development (`Seed:DemoUsers`). Password for all: `Demo@12345`.

| Email | Role | Notes |
| --- | --- | --- |
| admin@pcm.local | Admin | Approves, rejects, suspends and reinstates sellers |
| seller1@pcm.local | Seller | Store "Tech Hub", Approved |
| seller2@pcm.local | Seller | Store "Byte Corner", Pending (shows the review banner) |
| customer@pcm.local | Customer | Has one default address |

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

## Authentication and authorization

| Endpoint | Who | Notes |
| --- | --- | --- |
| `POST /api/v1/auth/register/customer` | Anonymous | Returns tokens (201) |
| `POST /api/v1/auth/register/seller` | Anonymous | Seller starts `Pending` |
| `POST /api/v1/auth/login` | Anonymous | 5 failures in a row lock the account for 15 minutes |
| `POST /api/v1/auth/refresh` | Anonymous | Rotates the refresh token |
| `POST /api/v1/auth/logout` | Signed in | Revokes the refresh token |
| `GET /api/v1/auth/me` | Signed in | Roles and seller status from the database |
| `/api/v1/account/addresses` (CRUD, `PUT {id}/default`) | Customer | Owner taken from the token; other users' ids return 404 |
| `/api/v1/seller/profile` (GET, PUT) | Seller, any status | Description and phone are editable; store name is fixed |
| `GET /api/v1/admin/sellers`, `POST {id}/approve, reject, suspend, reinstate` | Admin | Invalid transitions return 409 |

- **Tokens**: HS256 JWT access token (60 minutes) + opaque refresh token (64 random bytes, 30 days). Only the SHA-256 hash of a refresh token is stored.
- **Rotation and reuse detection**: every refresh revokes the old token and links it to the new one (`ReplacedByTokenId`). If an already-rotated token is presented again, every token issued after it is revoked, so a stolen token is useless after the real owner refreshes. The rotation uses a conditional `UPDATE ... WHERE RevokedAt IS NULL` inside a transaction, so two concurrent refreshes with the same token cannot both succeed.
- **Seller status state machine**: `Pending -> Approved | Rejected`, `Approved -> Suspended`, `Suspended -> Approved` (reinstate). Rejecting or suspending revokes the seller's refresh tokens. Sellers can sign in in any status; selling endpoints (Phase 4) use the `ApprovedSeller` policy, which checks the status in the database on every request instead of trusting the token claim.
- **One transaction across Identity and domain tables**: `AuthDbContext` (Identity) and `PcPartsContext` share one connection per request, and the Unit of Work enlists both in its transaction (e.g. user + seller profile at registration).
- **Rate limiting**: 10 requests per minute per IP on login, register and refresh (429 ProblemDetails).
- **UI session**: the MVC app keeps both tokens inside its encrypted, HttpOnly auth cookie (never in browser storage). A `DelegatingHandler` adds the bearer token to every API call, refreshes it shortly before it expires (or after a 401), and signs the user out if the refresh fails.

## Design decisions (so far)

- **Database-First from a versioned script**: the schema is reviewed as SQL (types, constraints, filtered indexes) and the entities are generated from it.
- **Database-level safety nets**: CHECK constraints (e.g. `ReservedStock <= Stock` prevents overselling), filtered unique indexes, rowversion concurrency tokens.
- **Audit and soft delete in one place**: an EF Core `SaveChangesInterceptor` sets `CreatedAt`/`UpdatedAt` and turns deletes into `IsDeleted = 1`; a named global query filter hides deleted rows.
- **Repositories never save**: the Unit of Work commits once per business operation and exposes transactions without leaking EF Core types.
- **Consistent errors**: every error is RFC 9457 ProblemDetails with `traceId` and `correlationId`; validation errors are 400 with field names.
- **No secrets in the repo**: user-secrets locally, environment variables in Docker and CI.

## Roadmap

1. ~~Foundation~~ ✔
2. ~~Auth: Identity, JWT + refresh tokens, roles, seller approval~~ ✔
3. Catalog: categories, specs, products, search and filters, seed data
4. Offers and inventory
5. Cart and transactional checkout
6. MVC UI
7. Polish: tests, caching, diagrams, screenshots
