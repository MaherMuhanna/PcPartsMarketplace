# PC Parts Marketplace

Multi-vendor marketplace for computer parts (RAM, GPUs, CPUs, storage): an ASP.NET Core Web API plus an ASP.NET Core MVC frontend, on SQL Server with EF Core (Database-First), Repository + Unit of Work.

> Status: **Phase 5 (Cart and checkout)** in progress. See the roadmap below.

## Tech stack

.NET 10 / C# 14 · ASP.NET Core (Controllers + MVC) · ASP.NET Core Identity + JWT · SQL Server 2025 · EF Core 10 (Database-First) · FluentValidation · Serilog · OpenAPI + Scalar · xUnit v3 · NSubstitute · Testcontainers · Docker Compose · GitHub Actions

## Solution layout

```
Database/               schema.sql (tables, source of truth), seed.sql (roles, brands, catalog), reset.sql
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

1. Create the database, then load the seed data (roles, 17 brands, 7 categories, 37 products):
   ```powershell
   sqlcmd -S localhost -E -C -i Database\schema.sql
   sqlcmd -S localhost -E -C -i Database\seed.sql
   ```
   (or open each file in SSMS and press F5, schema first). Both scripts are idempotent; to rebuild from scratch run `Database/reset.sql` first.

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
| seller1@pcm.local | Seller | Store "Tech Hub", Approved; 24 offers (some low or out of stock) and one Pending and one Rejected product proposal |
| seller2@pcm.local | Seller | Store "Byte Corner", Pending (shows the review banner) |
| seller3@pcm.local | Seller | Store "Circuit Store", Approved; 12 offers on the same products as Tech Hub at different prices |
| customer@pcm.local | Customer | One default address, 5 demo orders (Pending split between two sellers, Confirmed, Shipped, Delivered, Cancelled) and 2 items in the cart |

The demo proposals and offers need the catalog from `seed.sql`; if the API started before you ran it, restart the API. Seeding is idempotent: a seller who already has offers is skipped, and demo orders are skipped once the customer has any order. Demo orders go through the real cart, checkout and status services, so their stock reservations and movements are real.

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

Integration tests start a disposable SQL Server 2025 container (Docker must be running) and build it from the same `schema.sql` and `seed.sql`.

## Database-First workflow

1. Change `Database/schema.sql`.
2. Apply it (`reset.sql`, then `schema.sql`, then `seed.sql` in development).
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
- **Rate limiting**: 10 requests per minute per client IP on login, register and refresh (429 ProblemDetails). Browser traffic reaches the API through the MVC UI, so the UI forwards the browser's IP in `X-Forwarded-For` and the API accepts that header only from trusted proxies (`ForwardedHeaders:KnownProxies`; loopback by default, the UI container's fixed IP in Docker). Without this, every user would share the UI server's limit.
- **UI session**: the MVC app keeps both tokens inside its encrypted, HttpOnly auth cookie (never in browser storage). A `DelegatingHandler` adds the bearer token to every API call, refreshes it shortly before it expires (or after a 401), and signs the user out if the refresh fails.

## Catalog

| Endpoint | Who | Notes |
| --- | --- | --- |
| `GET /api/v1/categories` | Anyone | Category tree; products live on leaves |
| `GET /api/v1/categories/{id}/attributes` | Anyone | Attributes of a category with option ids (for product forms) |
| `GET /api/v1/products` | Anyone | Search, filters, sort, paging (see below) |
| `GET /api/v1/products/filters?category=` | Anyone | Sidebar data: breadcrumb, subcategories, brand counts, option counts, number ranges |
| `GET /api/v1/products/{id}`, `GET /api/v1/products/by-slug/{slug}` | Anyone | Product page: specs, images, offers (cheapest first) |
| `/api/v1/admin/categories` (POST, PUT, DELETE), `.../{id}/attributes` | Admin | 409 when a rule would break (e.g. deleting a used attribute) |
| `/api/v1/admin/attributes/{id}/options` (POST, PUT, DELETE) | Admin | Enum attributes only |
| `/api/v1/admin/brands` (POST, PUT, DELETE) | Admin | Brands with products cannot be deleted |
| `/api/v1/admin/products` (GET, POST, PUT), `{id}/activate, deactivate, approve, reject` | Admin | Review queue: `?status=Pending` |
| `/api/v1/seller/products/proposals` (GET, POST, PUT) | Approved seller | Own proposals only; a rejected one can be fixed and resubmitted |

Search URL, built from slugs so links are readable and shareable:

```
/api/v1/products?category=ram&brand=corsair,kingston&memory-type=ddr5&capacity=32-64&sort=price
```

- `q` keyword (name or model number), `category` (a parent includes its subcategories), `brand`, `minPrice`, `maxPrice`, `sort` (`newest`, `name`, `price`, `-price`), `page`, `pageSize`.
- Any other key is a spec filter named after an attribute slug of the leaf category. Enum: comma-separated option slugs (OR). Number: `min-max`, `min-` or `-max`. Different attributes combine with AND.
- Attributes are typed (Text, Number, Enum), so a new category needs rows, not schema changes. Number filters run as range queries on `ProductSpecValue.NumberValue` (indexed).
- Seller proposals are `Product` rows with `Status = Pending`; only Approved and active products are public.

## Offers and inventory

One master product, many seller offers: each approved seller sells a catalog product in a condition (New or Used) at their own price and stock.

| Endpoint | Who | Notes |
| --- | --- | --- |
| `GET /api/v1/seller/offers` | Approved seller | `q`, `condition`, `stock=all\|in\|low\|out`, `sort=newest\|name\|price\|-price\|available`, paging |
| `GET /api/v1/seller/offers/low-stock` | Approved seller | Available at or below the offer's low-stock threshold |
| `GET /api/v1/seller/offers/{id}` | Approved seller | Own offers only (404 otherwise) |
| `POST /api/v1/seller/offers` | Approved seller | `{ productId, condition, price, stock, lowStockThreshold? }`; published products only; one per product + condition |
| `PUT /api/v1/seller/offers/{id}` | Approved seller | `{ price, lowStockThreshold }` |
| `PATCH /api/v1/seller/offers/{id}/stock` | Approved seller | `{ type: Restock\|Adjustment, change, note }` |
| `DELETE /api/v1/seller/offers/{id}` | Approved seller | Soft delete; 409 while open orders hold reserved stock |
| `GET /api/v1/seller/offers/{id}/movements` | Approved seller | Stock history, newest first |

- **Available = Stock - ReservedStock**. Reserved units belong to placed orders (Phase 5), so stock can never drop below them (409), and the database CHECK `ReservedStock <= Stock` backs this up.
- **Every stock change is audited**: `OfferStock.Apply` is the single place that changes `Offer.Stock`, and it appends a `StockMovement` (reason, signed change, stock after, user, note) in the same SaveChanges. Initial stock is recorded as a Restock.
- **Optimistic concurrency**: `Offer.RowVersion` is a concurrency token. On a conflict the service discards its tracked state, reloads the offer and re-applies the change once (so the rules are checked against the new numbers); a second conflict returns 409.
- **Low stock** means available stock at or below the seller's own threshold (default 5).
- The MVC UI has "My offers" (stock tabs, filters, stock modal, history) and a "Sell this" button on product pages for approved sellers.

## Cart, checkout and orders

One cart per customer; lines reference offers (not products) and always show the current price and stock. Checkout creates one order with one sub-order per seller, paid cash on delivery.

| Endpoint | Who | Notes |
| --- | --- | --- |
| `GET /api/v1/cart`, `GET /api/v1/cart/summary` | Customer | Lines grouped by seller, issues per line (`Unavailable`, `InsufficientStock`), totals, `canCheckout` |
| `POST /api/v1/cart/items` | Customer | `{ offerId, quantity }`; adding the same offer again increases the line |
| `PUT /api/v1/cart/items/{id}`, `DELETE /api/v1/cart/items/{id}`, `DELETE /api/v1/cart` | Customer | Quantity 1 to 10 and not above available stock |
| `POST /api/v1/orders/checkout` | Customer | `{ addressId, items: [{ cartItemId, unitPrice, quantity }] }` as shown to the customer; 201 + order |
| `GET /api/v1/orders`, `GET /api/v1/orders/{id}` | Customer | Own orders only (404 otherwise) |
| `POST /api/v1/orders/{id}/cancel`, `POST /api/v1/orders/{id}/seller-orders/{sellerOrderId}/cancel` | Customer | Whole order or one seller's part; Pending or Confirmed only |
| `GET /api/v1/seller/orders`, `GET /api/v1/seller/orders/{id}` | Approved or suspended seller | Own sub-orders; `status` filter |
| `PATCH /api/v1/seller/orders/{id}/status` | Approved or suspended seller | `{ status, reason }`: Confirmed, Shipped, Delivered, or Cancelled with a reason |
| `GET /api/v1/admin/orders`, `GET /api/v1/admin/orders/{id}` | Admin | `status`, `q` (order number) |
| `PATCH /api/v1/admin/orders/{id}/seller-orders/{sellerOrderId}/status` | Admin | Same moves as sellers |

- **Transactional checkout**: one `IUnitOfWork` transaction locks the cart row (a double click waits, then finds the cart empty), re-reads every offer, compares price, quantity, availability and stock with what the customer saw, creates the order and sub-orders, reserves the stock (`Reserve` movements) and empties the cart. Any difference returns 409 with an `items` list of the changed lines (`PriceChanged`, `InsufficientStock`, `Unavailable`, `CartChanged`).
- **Concurrency**: `Offer.RowVersion` and `SellerOrder.RowVersion` are concurrency tokens. A conflict (or a SQL Server deadlock at checkout) is retried once on fresh data, then 409.
- **Sub-order flow**: `Pending -> Confirmed -> Shipped -> Delivered`; `Cancelled` only from Pending or Confirmed. Delivered sells the stock (Stock and ReservedStock drop); Cancelled releases it. Every change writes a `StockMovement` linked to the sub-order. The cancel reason records who cancelled (`Customer: ...`, `Seller: ...`, `Admin: ...`).
- **Snapshots**: order lines keep the product name, condition and price at checkout, and the order keeps a copy of the shipping address and recipient name. Editing or deleting an address later never changes where an existing order ships.
- **Auto-cancel of unconfirmed orders**: with cash on delivery an unconfirmed order only blocks stock, so a hosted `BackgroundService` cancels sub-orders still Pending after 3 days (`Orders:AutoCancel`), with the reason `System: Not confirmed within 3 days`, and releases their stock. A seller confirming at the same moment wins through the RowVersion check. Note for local demos: the demo Pending orders are cancelled 3 days after seeding.
- **Suspended sellers** cannot sell (offers hidden, offer endpoints 403) but still finish the sub-orders they accepted (`SellerCanFulfil` policy).
- The MVC UI has "Add to cart" on product pages, the cart, checkout, "My orders" with cancel, and "Orders" for sellers (status tabs and next-step buttons). Admin order pages come in Phase 6 (use Scalar).

## Design decisions (so far)

- **Database-First from a versioned script**: the schema is reviewed as SQL (types, constraints, filtered indexes) and the entities are generated from it.
- **Database-level safety nets**: CHECK constraints (e.g. `ReservedStock <= Stock` prevents overselling), filtered unique indexes, rowversion concurrency tokens.
- **Audit and soft delete in one place**: an EF Core `SaveChangesInterceptor` sets `CreatedAt`/`UpdatedAt` and turns deletes into `IsDeleted = 1`; a named global query filter hides deleted rows.
- **Repositories never save**: the Unit of Work commits once per business operation and exposes transactions without leaking EF Core types.
- **Consistent errors**: every error is RFC 9457 ProblemDetails with `traceId` and `correlationId`; validation errors are 400 with field names.
- **No secrets in the repo**: user-secrets locally, environment variables in Docker and CI.
- **Docker runs in `Development` on purpose**: it is the reviewer setup and needs Scalar and the demo accounts. A real deployment uses `Production` with secrets from environment variables or a vault.
- **Product images are URLs in V1** (http(s) or a site path). Upload to Azure Blob Storage is planned for V2.
- **No status history table in V1**: each sub-order keeps a timestamp per step and the cancel reason records who cancelled. A full audit trail (who moved which status, when) is planned for V2.

## Roadmap

1. ~~Foundation~~ ✔
2. ~~Auth: Identity, JWT + refresh tokens, roles, seller approval~~ ✔
3. ~~Catalog: categories, specs, products, search and filters, seed data~~ ✔
4. ~~Offers and inventory: seller offers, stock movements, low stock~~ ✔
5. Cart and transactional checkout (in progress)
6. MVC UI
7. Polish: tests, caching, diagrams, screenshots
