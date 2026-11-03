# Graph Report - PcPartsMarketplace  (2026-11-04)

## Corpus Check
- 372 files · ~100,842 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 9 file(s) not represented in the graph (top: (none) 5, .props 2, .example 1)

## Summary
- 3228 nodes · 10363 edges · 192 communities (124 shown, 68 thin omitted)
- Extraction: 89% EXTRACTED · 11% INFERRED · 0% AMBIGUOUS · INFERRED: 1093 edges (avg confidence: 0.85)
- Token cost: 319,005 input · 0 output

## Community Hubs (Navigation)
- API Controllers Core
- Delete Endpoints & Auth
- Cross-Layer Namespaces
- Cart API & Integration Tests
- Infrastructure Services & Mappings
- Repository & Domain Layers
- Product Management & Categories
- Admin Categories CRUD
- DTO Namespaces & Filters
- Order Listing (Admin/Seller/Customer)
- Product Form Builder
- NuGet Dependencies
- Addresses & Seller Profile
- Catalog Query Keys
- Demo Data Seeding
- Product Entity & Queries
- CI Workflow
- Soft Delete & Categories
- Spec Options & Category Updates
- Output Caching & Background Jobs
- Category Tree
- Catalog Filters DTOs
- Spec Attributes Service
- Brand Entity & Read Models
- Product Status & Review
- Refresh Tokens
- Seller Order Entity
- App Exceptions & Category Service
- Pending Order Expiry & Checkout
- UI Account & Login
- UI Addresses Controller
- Integration Test Fixture (SQL Container)
- Validation Tests
- Brands
- Auth DTOs
- Order Entity
- Offer Entity & Concurrency
- Unit of Work & Address Service
- Screenshots Workflow
- Address Entity
- Seller Offer Queries
- Auth Endpoint Tests
- EF Core DbContext
- Identity Auth DbContext
- JWT Auth Setup
- Seller Profile Entity
- Order Item Entity
- Address Requests
- README & Product Icons
- Product Editor
- FluentValidation Validators
- Shared Framework Imports
- Dashboard Repository
- UI Token Handler Tests
- Architecture Tests & Regex
- Offer Stock Changes
- Spec Attribute Entity
- Product Image & Spec Requests
- Checkout Requests
- Customer Directory & Seller Orders
- Order Mappings
- Order Service Interfaces & Admin Views
- Offer Stock Domain Logic
- Catalog Search Criteria
- Middleware & Interceptor Namespaces
- Stock Movement Entity
- Admin Products UI
- Cart Entity
- Offer Condition & Seller Offer Query
- Seller Status Lifecycle
- Playwright Screenshot Script
- Seller UI Screenshots
- Seller Order Status Transitions
- API Exception Handling (UI)
- Auditable Entities & Spec Options
- Cart Item Entity
- Product Catalog Service
- Create Offer
- Seller Admin Queries
- Seller Admin Service
- Claims & Fake Auth
- Validators & View Models
- Home & Catalog Screenshots
- Cart & Checkout Screenshots
- Admin Sellers Endpoint
- Client IP Forwarding
- Order & Cart Validators
- Spec Fields View Models
- Controller Rules Tests
- Spec Data Types
- Paging Queries
- UI API Token Handling
- Admin Screenshots
- HTTP Handler Tests
- Validation Rule Helpers
- Dashboard DTOs & Service
- Offer DTOs & Stock Movements
- Demo Accounts
- Offer Stock Tests
- API Launch Settings
- Checkout Rules
- Cart Service
- Global Exception Handler
- OpenAPI Setup
- Generic Repository
- Docker Compose: App
- Docker Compose: SQL Server
- Dashboard Controllers
- Product Spec Values
- Domain Enums
- Product List Query
- Seller Offer Service
- UI Launch Settings
- Audit Interceptor
- API Problem Details
- Product Image Entity
- SellerStatusRequirement
- ui
- environment
- pcm
- ProductFormTests.Pager_collapses_long_ra
- NavigationExtensions
- AccountValidatorsTests
- CartController
- StoreController.Index()
- OfferCreateViewModel
- CheckoutFormModel
- api
- healthcheck
- CartDtos.cs
- OfferEditViewModel
- SellerOffersViewModel
- CorrelationIdMiddleware.cs
- BrandListQuery
- DisplayExtensions
- coverage_gate.py
- CartMappingTests
- services
- AuthConstants.cs
- CheckoutViewModel
- ViewModels.cs
- Category Tree with Leaf Badges
- Products/Details.cshtml
- Roles and Security (Admin, Seller, Custo
- AddressListViewModel
- AdminBrandsViewModel
- AdminCategoriesViewModel
- AdminOrdersViewModel
- AdminProductsViewModel
- AdminSellersViewModel
- AttributeFieldsModel
- CatalogViewModel
- CategoryTreeNodesModel
- CheckoutViewModel
- ErrorViewModel
- HomeViewModel
- int
- OfferHistoryViewModel
- OfferProductPickerViewModel
- OrdersViewModel
- PagerViewModel
- PCM.Infrastructure.DTO.Catalog.ProductMa
- Register.cshtml
- Form.cshtml
- AdminProducts/Create.cshtml
- AdminProducts/Edit.cshtml
- SellerDashboard/Index.cshtml
- SellerOffers/Create.cshtml
- SellerOffers/Edit.cshtml
- SellerOffers/Index.cshtml
- _StockModal.cshtml
- SellerOrders/Index.cshtml
- SellerProposals/Create.cshtml
- SellerProposals/Edit.cshtml
- SellerProposals/Index.cshtml
- _ProductCard.cshtml
- _ProductForm.cshtml
- _ProductStatusBadge.cshtml
- _SellerStatusBanner.cshtml
- _SpecFields.cshtml
- _StatCard.cshtml
- Store/Index.cshtml

## God Nodes (most connected - your core abstractions)
1. `PagedResponse` - 95 edges
2. `PCM.DomainEntities.DBEntities` - 77 edges
3. `IUnitOfWork` - 64 edges
4. `PcPartsContext` - 63 edges
5. `ProductManageDto` - 63 edges
6. `PCM.Infrastructure.Services` - 62 edges
7. `Product` - 60 edges
8. `PCM.Infrastructure.DTO.Common` - 60 edges
9. `Offer` - 59 edges
10. `OfferDto` - 50 edges

## Surprising Connections (you probably didn't know these)
- `CPU product icon (Processor)` --references--> `Product Categories (CPU, GPU, RAM, Storage, Motherboard, PSU)`  [INFERRED]
  PCM.UI/wwwroot/images/products/cpu.svg → README.md
- `GPU product icon (Graphics card)` --references--> `Product Categories (CPU, GPU, RAM, Storage, Motherboard, PSU)`  [INFERRED]
  PCM.UI/wwwroot/images/products/gpu.svg → README.md
- `Motherboard product icon` --references--> `Product Categories (CPU, GPU, RAM, Storage, Motherboard, PSU)`  [INFERRED]
  PCM.UI/wwwroot/images/products/motherboard.svg → README.md
- `Power supply product icon` --references--> `Product Categories (CPU, GPU, RAM, Storage, Motherboard, PSU)`  [INFERRED]
  PCM.UI/wwwroot/images/products/power-supply.svg → README.md
- `RAM product icon (Memory)` --references--> `Product Categories (CPU, GPU, RAM, Storage, Motherboard, PSU)`  [INFERRED]
  PCM.UI/wwwroot/images/products/ram.svg → README.md

## Import Cycles
- None detected.

## Hyperedges (group relationships)
- **Product category icon set** — pcm_ui_wwwroot_images_products_cpu, pcm_ui_wwwroot_images_products_gpu, pcm_ui_wwwroot_images_products_motherboard, pcm_ui_wwwroot_images_products_power_supply, pcm_ui_wwwroot_images_products_ram, pcm_ui_wwwroot_images_products_storage, pcm_ui_wwwroot_images_products_placeholder [EXTRACTED 0.95]
- **Split-order checkout with stock reservation and concurrency** — readme_checkout_flow, readme_split_orders, readme_stock_reservation, readme_optimistic_concurrency [EXTRACTED 0.95]
- **Browse, compare offers, sign in to buy** — docs_screenshots_01_home_shop_by_category, docs_screenshots_02_catalog_filters_product_grid, docs_screenshots_03_product_offers_offers_table, docs_screenshots_04_login_demo_accounts_signin_form [INFERRED 0.75]
- **Cart to checkout to order tracking flow** — docs_screenshots_05_cart, docs_screenshots_06_checkout, docs_screenshots_07_orders, docs_screenshots_08_order_split_by_seller [INFERRED 0.85]
- **Split-by-seller pattern across cart, checkout, and order views** — docs_screenshots_05_cart_seller_grouped_cart, docs_screenshots_06_checkout_shipment_per_seller, docs_screenshots_08_order_split_by_seller_multi_seller_order [INFERRED 0.85]
- **Seller workflow screens** — docs_screenshots_09_seller_dashboard, docs_screenshots_10_seller_offers, docs_screenshots_11_seller_orders [INFERRED 0.85]
- **Admin moderation of sellers, products and categories** — docs_screenshots_13_admin_sellers_approve_seller, docs_screenshots_14_admin_review_queue_product_moderation, docs_screenshots_15_admin_categories_category_tree [INFERRED 0.75]

## Communities (192 total, 68 thin omitted)

### Community 0 - "API Controllers Core"
Cohesion: 0.06
Nodes (19): AddressesController, AdminBrandsController, AdminCategoriesController, AdminOrdersController, AdminProductsController, AdminSellersController, AdminSpecOptionsController, AuthController (+11 more)

### Community 1 - "Delete Endpoints & Auth"
Cohesion: 0.05
Nodes (11): IAuthService, IProductCatalogService, ISellerOfferService, DashboardApiClient, IDashboardApiClient, CatalogApiClient, ISellerOffersApiClient, OfferListFilter (+3 more)

### Community 2 - "Cross-Layer Namespaces"
Cohesion: 0.09
Nodes (17): PCM.UI.Catalog, PCM.UI.Extensions, PCM.API.Security, PCM.API.Caching, PCM.Infrastructure.DTO.Sellers, PCM.Infrastructure.DTO.Catalog, PCM.Infrastructure.Security, PCM.UI.Models (+9 more)

### Community 3 - "Cart API & Integration Tests"
Cohesion: 0.07
Nodes (19): CartController, AddCartItemRequest, OfferId, Quantity, CartDto, CartSummaryDto, UpdateCartItemRequest, Quantity (+11 more)

### Community 4 - "Infrastructure Services & Mappings"
Cohesion: 0.09
Nodes (17): PCM.Infrastructure.Tests.Orders, PCM.Infrastructure.Mappings, PCM.Infrastructure.Services, PCM.Infrastructure.DTO.Carts, PCM.Infrastructure.Orders, PCM.Infrastructure.Offers, PCM.Infrastructure.Seeding, PCM.DomainEntities.Enums (+9 more)

### Community 5 - "Repository & Domain Layers"
Cohesion: 0.08
Nodes (7): PCM.Repositories.Models, PCM.ArchitectureTests, PCM.DomainEntities.Context, PCM.DomainEntities.DBEntities, PCM.Repositories.Common, PCM.Repositories.IRepository, PCM.Repositories.Repository

### Community 6 - "Product Management & Categories"
Cohesion: 0.06
Nodes (22): CategoriesController, ProductManageDto, ProductUpsertRequest, BrandId, CategoryId, Description, Images, ModelNumber (+14 more)

### Community 7 - "Admin Categories CRUD"
Cohesion: 0.07
Nodes (12): CategoryDto, CreateCategoryRequest, Name, ParentId, SortOrder, CreateCategoryRequestValidator, AdminSellersApiClient, IAdminSellersApiClient (+4 more)

### Community 8 - "DTO Namespaces & Filters"
Cohesion: 0.08
Nodes (12): PCM.Infrastructure.DTO.Addresses, PCM.UI.Filters, PCM.UI.Security, PCM.Infrastructure.DTO.Common, PCM.API.IntegrationTests, PCM.Infrastructure.DTO.Brands, PCM.Infrastructure.DTO.Offers, PCM.Infrastructure.DTO.Auth (+4 more)

### Community 9 - "Order Listing (Admin/Seller/Customer)"
Cohesion: 0.07
Nodes (22): SellerOrdersController, PagedResponse, ChangeSellerOrderStatusRequest, Reason, Status, CheckoutProblems, OrderItemDto, OrderListItemDto (+14 more)

### Community 10 - "Product Form Builder"
Cohesion: 0.09
Nodes (25): BuiltRequest, ProductFormBuilder, SellerProposalsController, ImageRowViewModel, AltText, IsPreviewable, Url, ProductFormMode (+17 more)

### Community 11 - "NuGet Dependencies"
Cohesion: 0.05
Nodes (41): FluentValidation, FluentValidation.DependencyInjectionExtensions, Microsoft.AspNetCore.Authentication.JwtBearer, Microsoft.AspNetCore.Identity.EntityFrameworkCore, Microsoft.AspNetCore.Mvc.Testing, Microsoft.AspNetCore.OpenApi, Microsoft.EntityFrameworkCore.Design, Microsoft.EntityFrameworkCore.SqlServer (+33 more)

### Community 12 - "Addresses & Seller Profile"
Cohesion: 0.09
Nodes (11): SellerProfileController, AddressDto, AddressListQuery, SellerProfileDto, IAddressService, ISellerProfileService, SellerProfileService, AddressesApiClient (+3 more)

### Community 13 - "Catalog Query Keys"
Cohesion: 0.09
Nodes (13): CatalogQueryKeys, CatalogQuery, Empty, SpecKeys, CatalogViewModel, Categories, Error, Filters (+5 more)

### Community 14 - "Demo Data Seeding"
Cohesion: 0.09
Nodes (14): ApplicationUser, CreatedAt, FullName, UpdatedAt, DemoMovement, DemoOffer, DemoOfferSeeder, DemoLine (+6 more)

### Community 15 - "Product Entity & Queries"
Cohesion: 0.08
Nodes (26): Product, Brand, BrandId, Category, CategoryId, CreatedAt, Description, Id (+18 more)

### Community 16 - "CI Workflow"
Cohesion: 0.06
Nodes (37): name, runs-on, steps, env, name, runs-on, steps, env (+29 more)

### Community 17 - "Soft Delete & Categories"
Cohesion: 0.09
Nodes (20): ISoftDeletable, IsDeleted, Category, CreatedAt, Id, InverseParent, IsDeleted, Name (+12 more)

### Community 18 - "Spec Options & Category Updates"
Cohesion: 0.10
Nodes (16): SpecOptionRequest, SortOrder, Value, UpdateCategoryRequest, Name, SortOrder, UpdateCategoryRequestValidator, IAdminCatalogApiClient (+8 more)

### Community 19 - "Output Caching & Background Jobs"
Cohesion: 0.07
Nodes (17): PendingOrderExpiryJob, PendingOrderExpiryOptions, BatchSize, Enabled, IntervalMinutes, PendingDays, CachingSetup, CatalogCacheOptions (+9 more)

### Community 20 - "Category Tree"
Cohesion: 0.10
Nodes (22): CategoryTreeDto, AddressListViewModel, Addresses, AdminCategoriesViewModel, Attributes, Selected, Tree, AdminProductsViewModel (+14 more)

### Community 21 - "Catalog Filters DTOs"
Cohesion: 0.08
Nodes (15): AttributeFilterDto, BrandFacetDto, CatalogFiltersDto, CategorySummaryDto, OptionFacetDto, BrandSummaryDto, ProductImageDto, ProductListItemDto (+7 more)

### Community 22 - "Spec Attributes Service"
Cohesion: 0.11
Nodes (7): SpecAttributeDto, SpecOptionDto, ISpecAttributeService, AdminCatalogApiClient, ApiClientBase, Http, AuthApiClient

### Community 23 - "Brand Entity & Read Models"
Cohesion: 0.10
Nodes (17): Brand, CreatedAt, Id, IsDeleted, LogoUrl, Name, Products, Slug (+9 more)

### Community 24 - "Product Status & Review"
Cohesion: 0.09
Nodes (24): ProductStatus, Approved, Pending, Rejected, AdminProductListQuery, CategoryId, Q, Status (+16 more)

### Community 25 - "Refresh Tokens"
Cohesion: 0.10
Nodes (16): PCM.Infrastructure.Tests.Security, RefreshToken, CreatedAt, CreatedByIp, ExpiresAt, Id, InverseReplacedByToken, ReplacedByToken (+8 more)

### Community 26 - "Seller Order Entity"
Cohesion: 0.09
Nodes (21): SellerOrder, CancelledAt, CancelReason, ConfirmedAt, CreatedAt, DeliveredAt, Id, Order (+13 more)

### Community 27 - "App Exceptions & Category Service"
Cohesion: 0.18
Nodes (6): ConflictException, NotFoundException, CategoryService, SpecAttributeService, ISpecAttributeRepository, IRepository

### Community 28 - "Pending Order Expiry & Checkout"
Cohesion: 0.10
Nodes (19): PendingOrderExpiry, SellerOrderStatusChanger, CheckoutService, CustomerOrderService, IUnitOfWork, Addresses, Brands, Carts (+11 more)

### Community 29 - "UI Account & Login"
Cohesion: 0.09
Nodes (18): AccountController, AccountType, Customer, Seller, LoginViewModel, Email, Password, RememberMe (+10 more)

### Community 30 - "UI Addresses Controller"
Cohesion: 0.12
Nodes (11): AddressesController, AdminBrandsController, AdminSellersController, AddressFormViewModel, City, Details, Id, IsDefault (+3 more)

### Community 31 - "Integration Test Fixture (SQL Container)"
Cohesion: 0.09
Nodes (8): PublicCatalogCachePolicy, Program, HealthTests, ApiCollection, ApiFactory, ConnectionString, SellerProposalsEndpointTests, Ct

### Community 32 - "Validation Tests"
Cohesion: 0.15
Nodes (3): CreateOfferRequestValidator, ProductCatalogServiceTests, OfferValidatorsTests

### Community 33 - "Brands"
Cohesion: 0.13
Nodes (10): BrandsController, BrandDto, BrandRequest, LogoUrl, Name, BrandService, IBrandService, AdminBrandsViewModel (+2 more)

### Community 34 - "Auth DTOs"
Cohesion: 0.11
Nodes (17): AuthResponse, LoginRequest, Email, Password, RegisterCustomerRequest, Email, FullName, Password (+9 more)

### Community 35 - "Order Entity"
Cohesion: 0.11
Nodes (23): Order, CreatedAt, CustomerId, Id, OrderNumber, PaymentMethod, SellerOrders, ShipCity (+15 more)

### Community 36 - "Offer Entity & Concurrency"
Cohesion: 0.10
Nodes (21): Offer, CartItems, Condition, CreatedAt, Id, IsDeleted, LowStockThreshold, OrderItems (+13 more)

### Community 37 - "Unit of Work & Address Service"
Cohesion: 0.15
Nodes (4): AddressService, IUnitOfWorkTransaction, EfTransaction, AddressServiceTests

### Community 38 - "Screenshots Workflow"
Cohesion: 0.07
Nodes (27): name, runs-on, steps, timeout-minutes, concurrency, cancel-in-progress, group, jobs (+19 more)

### Community 39 - "Address Entity"
Cohesion: 0.10
Nodes (18): Address, City, CreatedAt, Details, Id, IsDefault, IsDeleted, Orders (+10 more)

### Community 40 - "Seller Offer Queries"
Cohesion: 0.11
Nodes (17): OfferStockFilter, All, InStock, Low, OutOfStock, SellerOfferFilter, Condition, Keyword (+9 more)

### Community 41 - "Auth Endpoint Tests"
Cohesion: 0.15
Nodes (4): AuthEndpointTests, Ct, BrandsEndpointTests, ProductEditorTests

### Community 42 - "EF Core DbContext"
Cohesion: 0.08
Nodes (19): PcPartsContext, UtcDateTimeConverter, Addresses, Brands, CartItems, Carts, Categories, Offers (+11 more)

### Community 43 - "Identity Auth DbContext"
Cohesion: 0.08
Nodes (18): AuthDbContext, ApplicationRole, DependencyInjection, UnitOfWork, Addresses, Brands, Carts, Categories (+10 more)

### Community 44 - "JWT Auth Setup"
Cohesion: 0.12
Nodes (11): AuthSetup, ForwardedHeadersSetup, JwtAccessTokenFactory, AccessToken, IAccessTokenFactory, JwtOptions, AccessTokenMinutes, Audience (+3 more)

### Community 45 - "Seller Profile Entity"
Cohesion: 0.11
Nodes (18): SellerProfile, ApprovedAt, ContactPhone, CreatedAt, Description, Id, IsDeleted, Offers (+10 more)

### Community 46 - "Order Item Entity"
Cohesion: 0.12
Nodes (14): OrderItem, Condition, CreatedAt, Id, LineTotal, Offer, OfferId, ProductName (+6 more)

### Community 47 - "Address Requests"
Cohesion: 0.11
Nodes (14): CreateAddressRequest, City, Details, IsDefault, Phone, Street, UpdateAddressRequest, City (+6 more)

### Community 48 - "README & Product Icons"
Cohesion: 0.10
Nodes (21): CPU product icon (Processor), GPU product icon (Graphics card), Motherboard product icon, Placeholder product icon (No image), Power supply product icon, RAM product icon (Memory), Storage product icon, Layered Architecture (UI, API, Infrastructure, Repositories, DomainEntities) (+13 more)

### Community 49 - "Product Editor"
Cohesion: 0.17
Nodes (6): ProductEditor, SlugGenerator, RequestValidationException, Errors, SellerProposalService, SlugGeneratorTests

### Community 50 - "FluentValidation Validators"
Cohesion: 0.14
Nodes (15): ProductReviewRequest, Note, AddressListQueryValidator, UpdateAddressRequestValidator, LoginRequestValidator, RefreshTokenRequestValidator, RegisterCustomerRequestValidator, AdminProductListQueryValidator (+7 more)

### Community 51 - "Shared Framework Imports"
Cohesion: 0.09
Nodes (4): PCM.UI.Tests, PCM.API.BackgroundJobs, CachePolicies, CacheTags

### Community 52 - "Dashboard Repository"
Cohesion: 0.17
Nodes (6): IDashboardRepository, DeliveredSalesRow, OfferStockCountsRow, RecentSubOrderRow, SubOrderStatusCountsRow, DashboardRepository

### Community 53 - "UI Token Handler Tests"
Cohesion: 0.16
Nodes (7): ApiTokenHandlerTests, Ct, TestSetup, Api, Auth, HttpContext, Refresh

### Community 54 - "Architecture Tests & Regex"
Cohesion: 0.13
Nodes (4): LayeringTests, AllowedProjectReferences, SourceTree, Root

### Community 55 - "Offer Stock Changes"
Cohesion: 0.23
Nodes (7): OfferDto, StockChangeRequest, Change, Note, Type, SellerOffersEndpointTests, Ct

### Community 56 - "Spec Attribute Entity"
Cohesion: 0.10
Nodes (17): SpecAttribute, Category, CategoryId, CreatedAt, DataType, Id, IsDeleted, IsFilterable (+9 more)

### Community 57 - "Product Image & Spec Requests"
Cohesion: 0.17
Nodes (13): ProductImageRequest, AltText, Url, SpecValueRequest, AttributeId, NumberValue, OptionId, TextValue (+5 more)

### Community 58 - "Checkout Requests"
Cohesion: 0.14
Nodes (11): CheckoutItemRequest, CartItemId, Quantity, UnitPrice, CheckoutRequest, AddressId, Items, IOrdersApiClient (+3 more)

### Community 59 - "Customer Directory & Seller Orders"
Cohesion: 0.18
Nodes (6): OrderCustomerDto, CustomerDirectory, ISellerOrderService, AdminOrderService, SellerOrderService, OrderRules

### Community 60 - "Order Mappings"
Cohesion: 0.19
Nodes (10): OrderMappings, OrderActor, Admin, Customer, Seller, System, OrderDetailsRow, OrderItemRow (+2 more)

### Community 61 - "Order Service Interfaces & Admin Views"
Cohesion: 0.10
Nodes (11): PCM.Infrastructure.DTO.Orders, AdminOrdersViewModel, Orders, Q, Sort, Status, AttributeFieldsModel, OrderDto (+3 more)

### Community 63 - "Catalog Search Criteria"
Cohesion: 0.14
Nodes (16): NumberFilter, OptionFilter, ProductListRow, ProductSearchCriteria, BrandIds, CategoryIds, Keyword, MaxPrice (+8 more)

### Community 64 - "Middleware & Interceptor Namespaces"
Cohesion: 0.12
Nodes (5): PCM.DomainEntities.Common, PCM.DomainEntities.Interceptors, PCM.Infrastructure, PCM.API.Middleware, PCM.API.Filters

### Community 65 - "Stock Movement Entity"
Cohesion: 0.13
Nodes (16): StockMovement, Change, CreatedAt, CreatedByUserId, Id, Note, Offer, OfferId (+8 more)

### Community 66 - "Admin Products UI"
Cohesion: 0.16
Nodes (6): ICatalogApiClient, AdminProductsController, CatalogController, HomeController, ProductFormController, ProductsController

### Community 67 - "Cart Entity"
Cohesion: 0.18
Nodes (9): Cart, CartItems, CreatedAt, CustomerId, Id, UpdatedAt, ICartRepository, CartLineRow (+1 more)

### Community 68 - "Offer Condition & Seller Offer Query"
Cohesion: 0.16
Nodes (11): OfferCondition, New, Used, SellerOfferListQuery, Condition, Q, Stock, OfferRules (+3 more)

### Community 69 - "Seller Status Lifecycle"
Cohesion: 0.19
Nodes (12): SellerStatus, Approved, Pending, Rejected, Suspended, SellerAction, Approve, Reinstate (+4 more)

### Community 70 - "Playwright Screenshot Script"
Cohesion: 0.12
Nodes (10): playwright, API, UI, VIEWPORT, dependencies, playwright@^1.50.0, description, name (+2 more)

### Community 71 - "Seller UI Screenshots"
Cohesion: 0.14
Nodes (17): Seller Dashboard Screenshot (Tech Hub), Add Offer / Propose a Product Actions, Inventory Stock Alert Cards, Orders To Handle KPI Cards, Recent Orders Table with Status Badges, Seller My Offers Screenshot, Offers Table (Stock, Reserved, Available), Row Actions Stock/Edit/History/Delete (+9 more)

### Community 72 - "Seller Order Status Transitions"
Cohesion: 0.23
Nodes (8): SellerOrderStatus, Cancelled, Confirmed, Delivered, Pending, Shipped, SellerOrderTransitions, SellerOrderTransitionsTests

### Community 73 - "API Exception Handling (UI)"
Cohesion: 0.16
Nodes (7): ApiException, FirstMessage, Problem, StatusCode, UserMessage, SellerOffersController, ModelStateExtensions

### Community 74 - "Auditable Entities & Spec Options"
Cohesion: 0.12
Nodes (14): IAuditable, CreatedAt, UpdatedAt, SpecOption, CreatedAt, Id, IsDeleted, ProductSpecValues (+6 more)

### Community 75 - "Cart Item Entity"
Cohesion: 0.15
Nodes (9): CartItem, Cart, CartId, CreatedAt, Id, Offer, OfferId, Quantity (+1 more)

### Community 76 - "Product Catalog Service"
Cohesion: 0.23
Nodes (4): ProductDetailsDto, ProductOfferDto, ProductCatalogService, ProductOfferRow

### Community 77 - "Create Offer"
Cohesion: 0.22
Nodes (6): CreateOfferRequest, Condition, LowStockThreshold, Price, ProductId, Stock

### Community 78 - "Seller Admin Queries"
Cohesion: 0.14
Nodes (11): SellerListQuery, Status, UpdateSellerProfileRequest, ContactPhone, Description, SellerListQueryValidator, UpdateSellerProfileRequestValidator, SellerSort (+3 more)

### Community 79 - "Seller Admin Service"
Cohesion: 0.26
Nodes (6): SellerAdminDto, ISellerAdminService, SellerAdminService, AdminSellersViewModel, Sellers, Status

### Community 80 - "Claims & Fake Auth"
Cohesion: 0.20
Nodes (4): ClaimsPrincipalExtensions, FakeAuthenticationService, SignIns, SignOuts

### Community 82 - "Home & Catalog Screenshots"
Cohesion: 0.15
Nodes (15): Home page screenshot, Hero banner with search, Top navbar (Catalog, search, Sign in, Create account), New in the catalog product cards, Shop by category tiles (CPU, GPU, RAM, Storage, Motherboard, Power Supply), Catalog RAM filters screenshot, Category tree sidebar with breadcrumbs, Faceted filters (price JOD, brand, memory type, capacity, speed) with counts (+7 more)

### Community 83 - "Cart & Checkout Screenshots"
Cohesion: 0.17
Nodes (15): Cart page screenshot, New/Used condition badge on listings, Cart items grouped by seller with per-seller subtotal, Cart summary panel (items, shipments, free shipping, total JOD), Checkout page screenshot, Cash on delivery payment, paid per seller on arrival, Per-seller shipment review blocks, Three-step checkout: address, review items, payment (+7 more)

### Community 84 - "Admin Sellers Endpoint"
Cohesion: 0.22
Nodes (5): SellerStatusReasonRequest, Reason, SellerStatusReasonRequestValidator, AdminSellersEndpointTests, Ct

### Community 85 - "Client IP Forwarding"
Cohesion: 0.18
Nodes (4): PCM.UI.Http, ClientIpForwardingHandler, ApiTokenHandler, FixedTimeProvider

### Community 86 - "Order & Cart Validators"
Cohesion: 0.23
Nodes (7): AddCartItemRequestValidator, CancelOrderRequestValidator, CartRules, ChangeSellerOrderStatusRequestValidator, CheckoutRequestValidator, UpdateCartItemRequestValidator, OrderValidatorsTests

### Community 87 - "Spec Fields View Models"
Cohesion: 0.15
Nodes (12): SpecFieldsModel, SpecFieldViewModel, AttributeId, DataType, HasValue, IsRequired, Name, NumberValue (+4 more)

### Community 89 - "Spec Data Types"
Cohesion: 0.15
Nodes (12): SpecDataType, Enum, Number, Text, SpecAttributeRequest, DataType, IsFilterable, IsRequired (+4 more)

### Community 90 - "Paging Queries"
Cohesion: 0.19
Nodes (11): PageQuery, Page, PageSize, Sort, AdminOrderListQuery, Q, Status, SellerOrderListQuery (+3 more)

### Community 91 - "UI API Token Handling"
Cohesion: 0.26
Nodes (3): UserDto, ApiTokens, AuthSession

### Community 92 - "Admin Screenshots"
Cohesion: 0.17
Nodes (12): Admin Sellers Screenshot, Approve / Reject / Suspend Seller Actions, Seller Status Filter Tabs (All/Pending/Approved/Suspended/Rejected), Sellers Table with Status Badges, Admin Product Review Queue Screenshot, Product Filter Bar (name, category, sort), Pending Product Proposed by Seller (Intel Core i5-14400F), Product Moderation (Approve/Reject/Edit) (+4 more)

### Community 93 - "HTTP Handler Tests"
Cohesion: 0.24
Nodes (5): RefreshTokenRequest, RefreshToken, RecordedRequest, StubHandler, Requests

### Community 95 - "Dashboard DTOs & Service"
Cohesion: 0.29
Nodes (8): AdminDashboardDto, RecentSubOrderDto, SellerDashboardDto, SubOrderStatusCountsDto, DashboardService, SellerDashboardViewModel, Dashboard, Profile

### Community 96 - "Offer DTOs & Stock Movements"
Cohesion: 0.21
Nodes (9): StockMovementDto, StockMovementListQuery, UpdateOfferRequest, LowStockThreshold, Price, UpdateOfferRequestValidator, OfferHistoryViewModel, Movements (+1 more)

### Community 97 - "Demo Accounts"
Cohesion: 0.20
Nodes (7): PCM.UI.Demo, DemoAccount, DemoAccounts, IsEnabled, List, Password, LoginViewModel

### Community 99 - "API Launch Settings"
Cohesion: 0.20
Nodes (9): ASPNETCORE_ENVIRONMENT, applicationUrl, commandName, environmentVariables, launchBrowser, launchUrl, profiles, https (+1 more)

### Community 102 - "Global Exception Handler"
Cohesion: 0.22
Nodes (4): GlobalExceptionHandler, CheckoutConflictException, Items, UnauthorizedException

### Community 104 - "Generic Repository"
Cohesion: 0.22
Nodes (3): Repository, Context, DbSet

### Community 105 - "Docker Compose: App"
Cohesion: 0.22
Nodes (9): condition, depends_on, entrypoint, image, networks, restart, volumes, db (+1 more)

### Community 106 - "Docker Compose: SQL Server"
Cohesion: 0.22
Nodes (9): environment, image, environment, networks, ports, volumes, ACCEPT_EULA, MSSQL_SA_PASSWORD (+1 more)

### Community 107 - "Dashboard Controllers"
Cohesion: 0.28
Nodes (3): AdminDashboardController, SellerDashboardController, IDashboardService

### Community 108 - "Product Spec Values"
Cohesion: 0.22
Nodes (9): ProductSpecValue, NumberValue, Product, ProductId, SpecAttribute, SpecAttributeId, SpecOption, SpecOptionId (+1 more)

### Community 109 - "Domain Enums"
Cohesion: 0.22
Nodes (8): PaymentMethod, COD, StockMovementReason, Adjustment, Release, Reserve, Restock, Sale

### Community 110 - "Product List Query"
Cohesion: 0.28
Nodes (7): ProductListQuery, Brand, Category, MaxPrice, MinPrice, Q, ProductListQueryValidator

### Community 112 - "UI Launch Settings"
Cohesion: 0.22
Nodes (8): ASPNETCORE_ENVIRONMENT, applicationUrl, commandName, environmentVariables, launchBrowser, profiles, https, $schema

### Community 114 - "API Problem Details"
Cohesion: 0.25
Nodes (7): CheckoutIssueDto, ApiProblem, Detail, Errors, Items, Status, Title

### Community 115 - "Product Image Entity"
Cohesion: 0.25
Nodes (8): ProductImage, AltText, CreatedAt, Id, Product, ProductId, SortOrder, Url

### Community 116 - "SellerStatusRequirement"
Cohesion: 0.33
Nodes (3): SellerStatusHandler, SellerStatusRequirement, AllowedStatuses

### Community 117 - "ui"
Cohesion: 0.33
Nodes (7): build, context, dockerfile, ui, build, depends_on, ports

### Community 118 - "environment"
Cohesion: 0.29
Nodes (7): environment, Api__BaseUrl, ASPNETCORE_ENVIRONMENT, ConnectionStrings__PcParts, ForwardedHeaders__KnownProxies__0, Jwt__SigningKey, environment

### Community 119 - "pcm"
Cohesion: 0.29
Nodes (7): subnet, config, networks, pcm, ipam, ipv4_address, networks

### Community 124 - "StoreController.Index()"
Cohesion: 0.33
Nodes (5): StoreController, StoreViewModel, ContactPhone, Description, Profile

### Community 125 - "OfferCreateViewModel"
Cohesion: 0.29
Nodes (7): OfferCreateViewModel, Condition, LowStockThreshold, Price, Product, ProductId, Stock

### Community 126 - "CheckoutFormModel"
Cohesion: 0.29
Nodes (7): CheckoutFormModel, AddressId, Items, CheckoutLineFormModel, CartItemId, Quantity, UnitPrice

### Community 127 - "api"
Cohesion: 0.33
Nodes (6): depends_on, networks, ports, condition, db-init, api

### Community 128 - "healthcheck"
Cohesion: 0.33
Nodes (6): healthcheck, interval, retries, start_period, test, timeout

### Community 129 - "CartDtos.cs"
Cohesion: 0.40
Nodes (3): CartIssues, CartItemDto, CartSellerGroupDto

### Community 130 - "OfferEditViewModel"
Cohesion: 0.33
Nodes (5): OfferEditViewModel, Id, LowStockThreshold, Offer, Price

### Community 131 - "SellerOffersViewModel"
Cohesion: 0.33
Nodes (6): SellerOffersViewModel, Condition, Offers, Q, Sort, Stock

### Community 137 - "services"
Cohesion: 0.50
Nodes (3): services, volumes, sqldata

### Community 138 - "AuthConstants.cs"
Cohesion: 0.50
Nodes (3): AppClaimTypes, Policies, Roles

### Community 139 - "CheckoutViewModel"
Cohesion: 0.50
Nodes (4): CheckoutViewModel, Addresses, Cart, SelectedAddressId

### Community 140 - "ViewModels.cs"
Cohesion: 0.50
Nodes (3): ErrorViewModel, RequestId, ShowRequestId

### Community 141 - "Category Tree with Leaf Badges"
Cohesion: 0.67
Nodes (3): Admin Categories and Specs Screenshot, Category Tree with Leaf Badges, Products and Spec Attributes Belong to Leaf Categories

### Community 143 - "Roles and Security (Admin, Seller, Custo"
Cohesion: 0.67
Nodes (3): Refresh-token Rotation with Reuse Detection, Roles and Security (Admin, Seller, Customer, JWT), Seller Approval Lifecycle

## Knowledge Gaps
- **719 isolated node(s):** `Enabled`, `PendingDays`, `IntervalMinutes`, `BatchSize`, `ProductsSeconds` (+714 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 995 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **68 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `PcPartsContext` connect `EF Core DbContext` to `Cart API & Integration Tests`, `Repository & Domain Layers`, `Admin Categories CRUD`, `Product Entity & Queries`, `Soft Delete & Categories`, `Brand Entity & Read Models`, `Refresh Tokens`, `Seller Order Entity`, `Order Entity`, `Offer Entity & Concurrency`, `Address Entity`, `Seller Offer Queries`, `Identity Auth DbContext`, `Seller Profile Entity`, `Order Item Entity`, `Dashboard Repository`, `Offer Stock Changes`, `Spec Attribute Entity`, `Stock Movement Entity`, `Cart Entity`, `Auditable Entities & Spec Options`, `Cart Item Entity`, `Generic Repository`, `Product Spec Values`, `Audit Interceptor`, `Product Image Entity`?**
  _High betweenness centrality (0.039) - this node is a cross-community bridge._
- **What connects `Enabled`, `PendingDays`, `IntervalMinutes` to the rest of the system?**
  _719 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `API Controllers Core` be split into smaller, more focused modules?**
  _Cohesion score 0.056074766355140186 - nodes in this community are weakly interconnected._
- **Why does `Product` connect `Product Entity & Queries` to `Delete Endpoints & Auth`, `Repository & Domain Layers`, `Product Management & Categories`, `Soft Delete & Categories`, `Catalog Filters DTOs`, `Brand Entity & Read Models`, `Product Status & Review`, `Offer Entity & Concurrency`, `EF Core DbContext`, `Seller Profile Entity`, `Product Editor`, `Dashboard Repository`, `Offer Stock Domain Logic`, `Middleware & Interceptor Namespaces`, `Cart Entity`, `Auditable Entities & Spec Options`, `Product Catalog Service`, `Checkout Rules`, `Product Spec Values`, `Product Image Entity`?**
  _High betweenness centrality (0.023) - this node is a cross-community bridge._
- **Should `Delete Endpoints & Auth` be split into smaller, more focused modules?**
  _Cohesion score 0.047021943573667714 - nodes in this community are weakly interconnected._
- **Why does `CartItem` connect `Cart Item Entity` to `Middleware & Interceptor Namespaces`, `Cart Entity`, `Offer Entity & Concurrency`, `Repository & Domain Layers`, `Checkout Rules`, `Cart Service`, `Admin Categories CRUD`, `Auditable Entities & Spec Options`, `EF Core DbContext`, `Dashboard Repository`?**
  _High betweenness centrality (0.022) - this node is a cross-community bridge._
- **Should `Cross-Layer Namespaces` be split into smaller, more focused modules?**
  _Cohesion score 0.0919080919080919 - nodes in this community are weakly interconnected._