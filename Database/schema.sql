/* =====================================================================
   PC Parts Marketplace: database schema (Database-First source of truth)
   ---------------------------------------------------------------------
   - Target: SQL Server 2022+ (developed on SQL Server 2025)
   - Run with SSMS (F5) or: sqlcmd -S localhost -E -C -i Database/schema.sql
   - Idempotent: if the schema already exists, the script skips everything.
     To rebuild from scratch, run Database/reset.sql first.
   - Tables only. Seed data is in Database/seed.sql (run it right after).
   - After any change here: re-run the script, then re-scaffold
     (see scripts/scaffold.ps1). Never edit scaffolded entities by hand.

   Conventions (see claude/decisions-log.md in the Claude Project):
   - int IDENTITY primary keys, singular PascalCase table names in dbo
   - All timestamps are UTC datetime2(3), default SYSUTCDATETIME()
   - [Audit] tables: CreatedAt, UpdatedAt (set by the EF interceptor),
     IsDeleted (soft delete, global query filter in EF)
   - Unique indexes on soft-delete tables are filtered WHERE IsDeleted = 0
   - Status/enum columns: nvarchar(20) + CHECK constraint
   ===================================================================== */

IF DB_ID(N'PcPartsMarketplace') IS NULL
    CREATE DATABASE PcPartsMarketplace;
GO

USE PcPartsMarketplace;
GO

-- Required for filtered indexes and indexed computed columns
-- (sqlcmd defaults QUOTED_IDENTIFIER to OFF).
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- Skip the whole script when the schema is already installed.
IF OBJECT_ID(N'dbo.Product', N'U') IS NOT NULL
BEGIN
    PRINT 'Schema already exists. Nothing to do (run Database/reset.sql to rebuild).';
    SET NOEXEC ON;
END
GO

/* =====================================================================
   1. Users and Auth
   ===================================================================== */

-- ---------------------------------------------------------------------
-- ASP.NET Core Identity tables (schema version 2, int keys).
-- Shape matches what IdentityDbContext<ApplicationUser, ApplicationRole, int>
-- expects. AuthDbContext maps these; PcPartsContext does not scaffold them.
-- ---------------------------------------------------------------------
CREATE TABLE dbo.AspNetRoles
(
    Id               int IDENTITY(1,1) NOT NULL,
    Name             nvarchar(256)     NULL,
    NormalizedName   nvarchar(256)     NULL,
    ConcurrencyStamp nvarchar(max)     NULL,
    CONSTRAINT PK_AspNetRoles PRIMARY KEY (Id)
);
CREATE UNIQUE INDEX RoleNameIndex ON dbo.AspNetRoles (NormalizedName) WHERE NormalizedName IS NOT NULL;
GO

CREATE TABLE dbo.AspNetUsers
(
    Id                   int IDENTITY(1,1) NOT NULL,
    -- Custom ApplicationUser columns
    FullName             nvarchar(100)     NOT NULL,
    CreatedAt            datetime2(3)      NOT NULL CONSTRAINT DF_AspNetUsers_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt            datetime2(3)      NULL,
    -- Standard Identity columns
    UserName             nvarchar(256)     NULL,
    NormalizedUserName   nvarchar(256)     NULL,
    Email                nvarchar(256)     NULL,
    NormalizedEmail      nvarchar(256)     NULL,
    EmailConfirmed       bit               NOT NULL,
    PasswordHash         nvarchar(max)     NULL,
    SecurityStamp        nvarchar(max)     NULL,
    ConcurrencyStamp     nvarchar(max)     NULL,
    PhoneNumber          nvarchar(max)     NULL,
    PhoneNumberConfirmed bit               NOT NULL,
    TwoFactorEnabled     bit               NOT NULL,
    LockoutEnd           datetimeoffset(7) NULL,
    LockoutEnabled       bit               NOT NULL,
    AccessFailedCount    int               NOT NULL,
    CONSTRAINT PK_AspNetUsers PRIMARY KEY (Id)
);
CREATE INDEX EmailIndex ON dbo.AspNetUsers (NormalizedEmail);
CREATE UNIQUE INDEX UserNameIndex ON dbo.AspNetUsers (NormalizedUserName) WHERE NormalizedUserName IS NOT NULL;
GO

CREATE TABLE dbo.AspNetRoleClaims
(
    Id         int IDENTITY(1,1) NOT NULL,
    RoleId     int               NOT NULL,
    ClaimType  nvarchar(max)     NULL,
    ClaimValue nvarchar(max)     NULL,
    CONSTRAINT PK_AspNetRoleClaims PRIMARY KEY (Id),
    CONSTRAINT FK_AspNetRoleClaims_AspNetRoles_RoleId FOREIGN KEY (RoleId) REFERENCES dbo.AspNetRoles (Id) ON DELETE CASCADE
);
CREATE INDEX IX_AspNetRoleClaims_RoleId ON dbo.AspNetRoleClaims (RoleId);
GO

CREATE TABLE dbo.AspNetUserClaims
(
    Id         int IDENTITY(1,1) NOT NULL,
    UserId     int               NOT NULL,
    ClaimType  nvarchar(max)     NULL,
    ClaimValue nvarchar(max)     NULL,
    CONSTRAINT PK_AspNetUserClaims PRIMARY KEY (Id),
    CONSTRAINT FK_AspNetUserClaims_AspNetUsers_UserId FOREIGN KEY (UserId) REFERENCES dbo.AspNetUsers (Id) ON DELETE CASCADE
);
CREATE INDEX IX_AspNetUserClaims_UserId ON dbo.AspNetUserClaims (UserId);
GO

CREATE TABLE dbo.AspNetUserLogins
(
    LoginProvider       nvarchar(128) NOT NULL,  -- 128 = Identity MaxLengthForKeys (keeps the PK under 900 bytes)
    ProviderKey         nvarchar(128) NOT NULL,
    ProviderDisplayName nvarchar(max) NULL,
    UserId              int           NOT NULL,
    CONSTRAINT PK_AspNetUserLogins PRIMARY KEY (LoginProvider, ProviderKey),
    CONSTRAINT FK_AspNetUserLogins_AspNetUsers_UserId FOREIGN KEY (UserId) REFERENCES dbo.AspNetUsers (Id) ON DELETE CASCADE
);
CREATE INDEX IX_AspNetUserLogins_UserId ON dbo.AspNetUserLogins (UserId);
GO

CREATE TABLE dbo.AspNetUserRoles
(
    UserId int NOT NULL,
    RoleId int NOT NULL,
    CONSTRAINT PK_AspNetUserRoles PRIMARY KEY (UserId, RoleId),
    CONSTRAINT FK_AspNetUserRoles_AspNetRoles_RoleId FOREIGN KEY (RoleId) REFERENCES dbo.AspNetRoles (Id) ON DELETE CASCADE,
    CONSTRAINT FK_AspNetUserRoles_AspNetUsers_UserId FOREIGN KEY (UserId) REFERENCES dbo.AspNetUsers (Id) ON DELETE CASCADE
);
CREATE INDEX IX_AspNetUserRoles_RoleId ON dbo.AspNetUserRoles (RoleId);
GO

CREATE TABLE dbo.AspNetUserTokens
(
    UserId        int           NOT NULL,
    LoginProvider nvarchar(128) NOT NULL,  -- 128 = Identity MaxLengthForKeys (keeps the PK under 900 bytes)
    Name          nvarchar(128) NOT NULL,
    Value         nvarchar(max) NULL,
    CONSTRAINT PK_AspNetUserTokens PRIMARY KEY (UserId, LoginProvider, Name),
    CONSTRAINT FK_AspNetUserTokens_AspNetUsers_UserId FOREIGN KEY (UserId) REFERENCES dbo.AspNetUsers (Id) ON DELETE CASCADE
);
GO

-- ---------------------------------------------------------------------
-- RefreshToken: hashed refresh tokens with a rotation chain.
-- ReplacedByTokenId links a rotated token to its successor, so reuse of an
-- already-rotated token can be detected and the whole chain revoked.
-- ---------------------------------------------------------------------
CREATE TABLE dbo.RefreshToken
(
    Id                int IDENTITY(1,1) NOT NULL,
    UserId            int               NOT NULL,
    TokenHash         char(64)          NOT NULL,  -- SHA-256 hex; the raw token is never stored
    ExpiresAt         datetime2(3)      NOT NULL,
    CreatedAt         datetime2(3)      NOT NULL CONSTRAINT DF_RefreshToken_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedByIp       varchar(45)       NULL,      -- long enough for IPv6
    RevokedAt         datetime2(3)      NULL,
    ReplacedByTokenId int               NULL,
    CONSTRAINT PK_RefreshToken PRIMARY KEY (Id),
    CONSTRAINT FK_RefreshToken_AspNetUsers_UserId FOREIGN KEY (UserId) REFERENCES dbo.AspNetUsers (Id) ON DELETE CASCADE,
    CONSTRAINT FK_RefreshToken_RefreshToken_ReplacedByTokenId FOREIGN KEY (ReplacedByTokenId) REFERENCES dbo.RefreshToken (Id)
);
CREATE UNIQUE INDEX UX_RefreshToken_TokenHash ON dbo.RefreshToken (TokenHash);
CREATE INDEX IX_RefreshToken_UserId ON dbo.RefreshToken (UserId);
GO

-- ---------------------------------------------------------------------
-- SellerProfile [Audit]: 1:1 with a Seller user; needs admin approval.
-- ---------------------------------------------------------------------
CREATE TABLE dbo.SellerProfile
(
    Id           int IDENTITY(1,1) NOT NULL,
    UserId       int               NOT NULL,
    StoreName    nvarchar(100)     NOT NULL,
    Description  nvarchar(500)     NULL,
    ContactPhone varchar(20)       NULL,
    Status       nvarchar(20)      NOT NULL CONSTRAINT DF_SellerProfile_Status DEFAULT N'Pending',
    StatusReason nvarchar(500)     NULL,     -- reason for rejection or suspension
    ApprovedAt   datetime2(3)      NULL,
    CreatedAt    datetime2(3)      NOT NULL CONSTRAINT DF_SellerProfile_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt    datetime2(3)      NULL,
    IsDeleted    bit               NOT NULL CONSTRAINT DF_SellerProfile_IsDeleted DEFAULT 0,
    CONSTRAINT PK_SellerProfile PRIMARY KEY (Id),
    CONSTRAINT FK_SellerProfile_AspNetUsers_UserId FOREIGN KEY (UserId) REFERENCES dbo.AspNetUsers (Id),
    CONSTRAINT CK_SellerProfile_Status CHECK (Status IN (N'Pending', N'Approved', N'Rejected', N'Suspended'))
);
CREATE UNIQUE INDEX UX_SellerProfile_UserId ON dbo.SellerProfile (UserId);
CREATE UNIQUE INDEX UX_SellerProfile_StoreName ON dbo.SellerProfile (StoreName) WHERE IsDeleted = 0;
CREATE INDEX IX_SellerProfile_Status ON dbo.SellerProfile (Status);
GO

-- ---------------------------------------------------------------------
-- Address [Audit]: customer shipping addresses; at most one default.
-- ---------------------------------------------------------------------
CREATE TABLE dbo.Address
(
    Id        int IDENTITY(1,1) NOT NULL,
    UserId    int               NOT NULL,
    City      nvarchar(100)     NOT NULL,
    Street    nvarchar(200)     NOT NULL,
    Details   nvarchar(200)     NULL,       -- building, floor, landmark
    Phone     varchar(20)       NOT NULL,
    IsDefault bit               NOT NULL CONSTRAINT DF_Address_IsDefault DEFAULT 0,
    CreatedAt datetime2(3)      NOT NULL CONSTRAINT DF_Address_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt datetime2(3)      NULL,
    IsDeleted bit               NOT NULL CONSTRAINT DF_Address_IsDeleted DEFAULT 0,
    CONSTRAINT PK_Address PRIMARY KEY (Id),
    CONSTRAINT FK_Address_AspNetUsers_UserId FOREIGN KEY (UserId) REFERENCES dbo.AspNetUsers (Id)
);
CREATE INDEX IX_Address_UserId ON dbo.Address (UserId);
-- Enforces "one default address per user" at the database level.
CREATE UNIQUE INDEX UX_Address_UserId_Default ON dbo.Address (UserId) WHERE IsDefault = 1 AND IsDeleted = 0;
GO

/* =====================================================================
   2. Catalog
   ===================================================================== */

-- Category [Audit]: tree of categories; products attach to leaf categories only.
CREATE TABLE dbo.Category
(
    Id        int IDENTITY(1,1) NOT NULL,
    ParentId  int               NULL,
    Name      nvarchar(100)     NOT NULL,
    Slug      varchar(100)      NOT NULL,
    SortOrder int               NOT NULL CONSTRAINT DF_Category_SortOrder DEFAULT 0,
    CreatedAt datetime2(3)      NOT NULL CONSTRAINT DF_Category_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt datetime2(3)      NULL,
    IsDeleted bit               NOT NULL CONSTRAINT DF_Category_IsDeleted DEFAULT 0,
    CONSTRAINT PK_Category PRIMARY KEY (Id),
    CONSTRAINT FK_Category_Category_ParentId FOREIGN KEY (ParentId) REFERENCES dbo.Category (Id)
);
CREATE UNIQUE INDEX UX_Category_Slug ON dbo.Category (Slug) WHERE IsDeleted = 0;
CREATE UNIQUE INDEX UX_Category_ParentId_Name ON dbo.Category (ParentId, Name) WHERE IsDeleted = 0;
CREATE INDEX IX_Category_ParentId ON dbo.Category (ParentId);
GO

-- SpecAttribute [Audit]: a spec a category defines, e.g. RAM > Capacity (GB).
CREATE TABLE dbo.SpecAttribute
(
    Id           int IDENTITY(1,1) NOT NULL,
    CategoryId   int               NOT NULL,
    Name         nvarchar(100)     NOT NULL,
    Slug         varchar(100)      NOT NULL,      -- query-string key in catalog filters, e.g. memory-type
    DataType     nvarchar(20)      NOT NULL,
    Unit         nvarchar(20)      NULL,
    IsFilterable bit               NOT NULL CONSTRAINT DF_SpecAttribute_IsFilterable DEFAULT 0,
    IsRequired   bit               NOT NULL CONSTRAINT DF_SpecAttribute_IsRequired DEFAULT 0,
    SortOrder    int               NOT NULL CONSTRAINT DF_SpecAttribute_SortOrder DEFAULT 0,
    CreatedAt    datetime2(3)      NOT NULL CONSTRAINT DF_SpecAttribute_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt    datetime2(3)      NULL,
    IsDeleted    bit               NOT NULL CONSTRAINT DF_SpecAttribute_IsDeleted DEFAULT 0,
    CONSTRAINT PK_SpecAttribute PRIMARY KEY (Id),
    CONSTRAINT FK_SpecAttribute_Category_CategoryId FOREIGN KEY (CategoryId) REFERENCES dbo.Category (Id),
    CONSTRAINT CK_SpecAttribute_DataType CHECK (DataType IN (N'Text', N'Number', N'Enum'))
);
CREATE UNIQUE INDEX UX_SpecAttribute_CategoryId_Name ON dbo.SpecAttribute (CategoryId, Name) WHERE IsDeleted = 0;
CREATE UNIQUE INDEX UX_SpecAttribute_CategoryId_Slug ON dbo.SpecAttribute (CategoryId, Slug) WHERE IsDeleted = 0;
GO

-- SpecOption [Audit]: allowed values of an Enum attribute, e.g. DDR4, DDR5.
CREATE TABLE dbo.SpecOption
(
    Id              int IDENTITY(1,1) NOT NULL,
    SpecAttributeId int               NOT NULL,
    Value           nvarchar(100)     NOT NULL,
    Slug            varchar(100)      NOT NULL,   -- query-string value in catalog filters, e.g. ddr5
    SortOrder       int               NOT NULL CONSTRAINT DF_SpecOption_SortOrder DEFAULT 0,
    CreatedAt       datetime2(3)      NOT NULL CONSTRAINT DF_SpecOption_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt       datetime2(3)      NULL,
    IsDeleted       bit               NOT NULL CONSTRAINT DF_SpecOption_IsDeleted DEFAULT 0,
    CONSTRAINT PK_SpecOption PRIMARY KEY (Id),
    CONSTRAINT FK_SpecOption_SpecAttribute_SpecAttributeId FOREIGN KEY (SpecAttributeId) REFERENCES dbo.SpecAttribute (Id)
);
CREATE UNIQUE INDEX UX_SpecOption_SpecAttributeId_Slug ON dbo.SpecOption (SpecAttributeId, Slug) WHERE IsDeleted = 0;
CREATE UNIQUE INDEX UX_SpecOption_SpecAttributeId_Value ON dbo.SpecOption (SpecAttributeId, Value) WHERE IsDeleted = 0;
GO

-- Brand [Audit]
CREATE TABLE dbo.Brand
(
    Id        int IDENTITY(1,1) NOT NULL,
    Name      nvarchar(100)     NOT NULL,
    Slug      varchar(100)      NOT NULL,          -- catalog filter value, e.g. ?brand=amd
    LogoUrl   nvarchar(500)     NULL,
    CreatedAt datetime2(3)      NOT NULL CONSTRAINT DF_Brand_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt datetime2(3)      NULL,
    IsDeleted bit               NOT NULL CONSTRAINT DF_Brand_IsDeleted DEFAULT 0,
    CONSTRAINT PK_Brand PRIMARY KEY (Id)
);
CREATE UNIQUE INDEX UX_Brand_Name ON dbo.Brand (Name) WHERE IsDeleted = 0;
CREATE UNIQUE INDEX UX_Brand_Slug ON dbo.Brand (Slug) WHERE IsDeleted = 0;
GO

-- Product [Audit]: one master product per real part. Seller proposals are
-- stored here with Status = 'Pending' until an admin approves them.
CREATE TABLE dbo.Product
(
    Id                 int IDENTITY(1,1) NOT NULL,
    CategoryId         int               NOT NULL,
    BrandId            int               NOT NULL,
    Name               nvarchar(200)     NOT NULL,
    ModelNumber        varchar(100)      NOT NULL,
    Slug               varchar(250)      NOT NULL,
    Description        nvarchar(max)     NULL,
    Status             nvarchar(20)      NOT NULL CONSTRAINT DF_Product_Status DEFAULT N'Approved',
    ProposedBySellerId int               NULL,      -- NULL when created by an admin
    ReviewNote         nvarchar(500)     NULL,      -- rejection reason
    IsActive           bit               NOT NULL CONSTRAINT DF_Product_IsActive DEFAULT 1,
    CreatedAt          datetime2(3)      NOT NULL CONSTRAINT DF_Product_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt          datetime2(3)      NULL,
    IsDeleted          bit               NOT NULL CONSTRAINT DF_Product_IsDeleted DEFAULT 0,
    CONSTRAINT PK_Product PRIMARY KEY (Id),
    CONSTRAINT FK_Product_Category_CategoryId FOREIGN KEY (CategoryId) REFERENCES dbo.Category (Id),
    CONSTRAINT FK_Product_Brand_BrandId FOREIGN KEY (BrandId) REFERENCES dbo.Brand (Id),
    CONSTRAINT FK_Product_SellerProfile_ProposedBySellerId FOREIGN KEY (ProposedBySellerId) REFERENCES dbo.SellerProfile (Id),
    CONSTRAINT CK_Product_Status CHECK (Status IN (N'Pending', N'Approved', N'Rejected'))
);
CREATE INDEX IX_Product_CategoryId_BrandId ON dbo.Product (CategoryId, BrandId);
-- Prevents the same physical part from being listed twice in the catalog.
CREATE UNIQUE INDEX UX_Product_BrandId_ModelNumber ON dbo.Product (BrandId, ModelNumber) WHERE IsDeleted = 0;
CREATE UNIQUE INDEX UX_Product_Slug ON dbo.Product (Slug) WHERE IsDeleted = 0;
CREATE INDEX IX_Product_Status ON dbo.Product (Status);
GO

-- ProductSpecValue: one typed value per (product, attribute). Owned child,
-- hard-deleted. Exactly one of the three value columns must be set.
CREATE TABLE dbo.ProductSpecValue
(
    ProductId       int           NOT NULL,
    SpecAttributeId int           NOT NULL,
    TextValue       nvarchar(200) NULL,
    NumberValue     decimal(18,4) NULL,
    SpecOptionId    int           NULL,
    CONSTRAINT PK_ProductSpecValue PRIMARY KEY (ProductId, SpecAttributeId),
    CONSTRAINT FK_ProductSpecValue_Product_ProductId FOREIGN KEY (ProductId) REFERENCES dbo.Product (Id) ON DELETE CASCADE,
    CONSTRAINT FK_ProductSpecValue_SpecAttribute_SpecAttributeId FOREIGN KEY (SpecAttributeId) REFERENCES dbo.SpecAttribute (Id),
    CONSTRAINT FK_ProductSpecValue_SpecOption_SpecOptionId FOREIGN KEY (SpecOptionId) REFERENCES dbo.SpecOption (Id),
    CONSTRAINT CK_ProductSpecValue_ExactlyOneValue CHECK (
          (CASE WHEN TextValue    IS NULL THEN 0 ELSE 1 END)
        + (CASE WHEN NumberValue  IS NULL THEN 0 ELSE 1 END)
        + (CASE WHEN SpecOptionId IS NULL THEN 0 ELSE 1 END) = 1)
);
-- Indexes used by spec filters (e.g. DDR5 + 32GB).
CREATE INDEX IX_ProductSpecValue_Attr_Number ON dbo.ProductSpecValue (SpecAttributeId, NumberValue) INCLUDE (ProductId);
CREATE INDEX IX_ProductSpecValue_Attr_Option ON dbo.ProductSpecValue (SpecAttributeId, SpecOptionId) INCLUDE (ProductId);
GO

-- ProductImage: owned child, hard-deleted. SortOrder 0 is the main image.
CREATE TABLE dbo.ProductImage
(
    Id        int IDENTITY(1,1) NOT NULL,
    ProductId int               NOT NULL,
    Url       nvarchar(500)     NOT NULL,
    AltText   nvarchar(200)     NULL,
    SortOrder int               NOT NULL CONSTRAINT DF_ProductImage_SortOrder DEFAULT 0,
    CreatedAt datetime2(3)      NOT NULL CONSTRAINT DF_ProductImage_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_ProductImage PRIMARY KEY (Id),
    CONSTRAINT FK_ProductImage_Product_ProductId FOREIGN KEY (ProductId) REFERENCES dbo.Product (Id) ON DELETE CASCADE
);
CREATE INDEX IX_ProductImage_ProductId_SortOrder ON dbo.ProductImage (ProductId, SortOrder);
GO

/* =====================================================================
   3. Offers and Inventory
   ===================================================================== */

-- Offer [Audit]: one seller selling one product in one condition.
-- Available to buy = Stock - ReservedStock (computed in queries).
CREATE TABLE dbo.Offer
(
    Id                int IDENTITY(1,1) NOT NULL,
    ProductId         int               NOT NULL,
    SellerId          int               NOT NULL,
    Condition         nvarchar(20)      NOT NULL CONSTRAINT DF_Offer_Condition DEFAULT N'New',
    Price             decimal(18,2)     NOT NULL,
    Stock             int               NOT NULL CONSTRAINT DF_Offer_Stock DEFAULT 0,
    ReservedStock     int               NOT NULL CONSTRAINT DF_Offer_ReservedStock DEFAULT 0,
    LowStockThreshold int               NOT NULL CONSTRAINT DF_Offer_LowStockThreshold DEFAULT 5,
    RowVersion        rowversion        NOT NULL,   -- optimistic concurrency token
    CreatedAt         datetime2(3)      NOT NULL CONSTRAINT DF_Offer_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt         datetime2(3)      NULL,
    IsDeleted         bit               NOT NULL CONSTRAINT DF_Offer_IsDeleted DEFAULT 0,
    CONSTRAINT PK_Offer PRIMARY KEY (Id),
    CONSTRAINT FK_Offer_Product_ProductId FOREIGN KEY (ProductId) REFERENCES dbo.Product (Id),
    CONSTRAINT FK_Offer_SellerProfile_SellerId FOREIGN KEY (SellerId) REFERENCES dbo.SellerProfile (Id),
    CONSTRAINT CK_Offer_Condition CHECK (Condition IN (N'New', N'Used')),
    CONSTRAINT CK_Offer_Price CHECK (Price > 0),
    CONSTRAINT CK_Offer_Stock CHECK (Stock >= 0),
    -- Last line of defence against overselling, even if application code has a bug.
    CONSTRAINT CK_Offer_ReservedStock CHECK (ReservedStock >= 0 AND ReservedStock <= Stock),
    CONSTRAINT CK_Offer_LowStockThreshold CHECK (LowStockThreshold >= 0)
);
CREATE UNIQUE INDEX UX_Offer_Product_Seller_Condition ON dbo.Offer (ProductId, SellerId, Condition) WHERE IsDeleted = 0;
CREATE INDEX IX_Offer_ProductId_Price ON dbo.Offer (ProductId, Price);
CREATE INDEX IX_Offer_SellerId ON dbo.Offer (SellerId);
GO

/* =====================================================================
   4. Cart and Orders
   (SellerOrder is created before StockMovement, which references it.)
   ===================================================================== */

-- Cart: one per customer.
CREATE TABLE dbo.Cart
(
    Id         int IDENTITY(1,1) NOT NULL,
    CustomerId int               NOT NULL,
    CreatedAt  datetime2(3)      NOT NULL CONSTRAINT DF_Cart_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt  datetime2(3)      NULL,
    CONSTRAINT PK_Cart PRIMARY KEY (Id),
    CONSTRAINT FK_Cart_AspNetUsers_CustomerId FOREIGN KEY (CustomerId) REFERENCES dbo.AspNetUsers (Id)
);
CREATE UNIQUE INDEX UX_Cart_CustomerId ON dbo.Cart (CustomerId);
GO

-- CartItem: references an offer, not a product. Hard-deleted.
CREATE TABLE dbo.CartItem
(
    Id        int IDENTITY(1,1) NOT NULL,
    CartId    int               NOT NULL,
    OfferId   int               NOT NULL,
    Quantity  int               NOT NULL,
    CreatedAt datetime2(3)      NOT NULL CONSTRAINT DF_CartItem_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt datetime2(3)      NULL,
    CONSTRAINT PK_CartItem PRIMARY KEY (Id),
    CONSTRAINT FK_CartItem_Cart_CartId FOREIGN KEY (CartId) REFERENCES dbo.Cart (Id) ON DELETE CASCADE,
    CONSTRAINT FK_CartItem_Offer_OfferId FOREIGN KEY (OfferId) REFERENCES dbo.Offer (Id),
    CONSTRAINT CK_CartItem_Quantity CHECK (Quantity BETWEEN 1 AND 10)
);
-- Same offer appears once per cart; adding it again increases the quantity.
CREATE UNIQUE INDEX UX_CartItem_CartId_OfferId ON dbo.CartItem (CartId, OfferId);
GO

-- Order: never deleted. OrderNumber is derived from Id (PCM-000123).
CREATE TABLE dbo.[Order]
(
    Id                int IDENTITY(1,1) NOT NULL,
    OrderNumber       AS (CAST(N'PCM-' + CASE WHEN Id < 1000000
                                              THEN RIGHT(N'000000' + CAST(Id AS nvarchar(10)), 6)
                                              ELSE CAST(Id AS nvarchar(10)) END AS nvarchar(20))) PERSISTED,
    CustomerId        int               NOT NULL,
    ShippingAddressId int               NOT NULL,
    Total             decimal(18,2)     NOT NULL,
    PaymentMethod     nvarchar(20)      NOT NULL CONSTRAINT DF_Order_PaymentMethod DEFAULT N'COD',
    CreatedAt         datetime2(3)      NOT NULL CONSTRAINT DF_Order_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt         datetime2(3)      NULL,
    CONSTRAINT PK_Order PRIMARY KEY (Id),
    CONSTRAINT FK_Order_AspNetUsers_CustomerId FOREIGN KEY (CustomerId) REFERENCES dbo.AspNetUsers (Id),
    CONSTRAINT FK_Order_Address_ShippingAddressId FOREIGN KEY (ShippingAddressId) REFERENCES dbo.Address (Id),
    CONSTRAINT CK_Order_PaymentMethod CHECK (PaymentMethod IN (N'COD')),
    CONSTRAINT CK_Order_Total CHECK (Total >= 0)
);
CREATE UNIQUE INDEX UX_Order_OrderNumber ON dbo.[Order] (OrderNumber);
CREATE INDEX IX_Order_CustomerId_CreatedAt ON dbo.[Order] (CustomerId, CreatedAt);
GO

-- SellerOrder: one sub-order per seller inside an order.
-- Flow: Pending -> Confirmed -> Shipped -> Delivered; Cancelled from Pending/Confirmed.
CREATE TABLE dbo.SellerOrder
(
    Id           int IDENTITY(1,1) NOT NULL,
    OrderId      int               NOT NULL,
    SellerId     int               NOT NULL,
    Status       nvarchar(20)      NOT NULL CONSTRAINT DF_SellerOrder_Status DEFAULT N'Pending',
    Subtotal     decimal(18,2)     NOT NULL,
    ConfirmedAt  datetime2(3)      NULL,
    ShippedAt    datetime2(3)      NULL,
    DeliveredAt  datetime2(3)      NULL,
    CancelledAt  datetime2(3)      NULL,
    CancelReason nvarchar(300)     NULL,
    CreatedAt    datetime2(3)      NOT NULL CONSTRAINT DF_SellerOrder_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt    datetime2(3)      NULL,
    RowVersion   rowversion        NOT NULL,   -- seller ships while customer cancels: one of them gets 409
    CONSTRAINT PK_SellerOrder PRIMARY KEY (Id),
    CONSTRAINT FK_SellerOrder_Order_OrderId FOREIGN KEY (OrderId) REFERENCES dbo.[Order] (Id),
    CONSTRAINT FK_SellerOrder_SellerProfile_SellerId FOREIGN KEY (SellerId) REFERENCES dbo.SellerProfile (Id),
    CONSTRAINT CK_SellerOrder_Status CHECK (Status IN (N'Pending', N'Confirmed', N'Shipped', N'Delivered', N'Cancelled')),
    CONSTRAINT CK_SellerOrder_Subtotal CHECK (Subtotal >= 0)
);
CREATE UNIQUE INDEX UX_SellerOrder_OrderId_SellerId ON dbo.SellerOrder (OrderId, SellerId);
CREATE INDEX IX_SellerOrder_SellerId_Status ON dbo.SellerOrder (SellerId, Status);
GO

-- OrderItem: append-only snapshot of what was bought, at the price paid.
CREATE TABLE dbo.OrderItem
(
    Id            int IDENTITY(1,1) NOT NULL,
    SellerOrderId int               NOT NULL,
    OfferId       int               NOT NULL,
    ProductName   nvarchar(200)     NOT NULL,
    Condition     nvarchar(20)      NOT NULL,
    UnitPrice     decimal(18,2)     NOT NULL,
    Quantity      int               NOT NULL,
    LineTotal     AS (UnitPrice * Quantity) PERSISTED,
    CreatedAt     datetime2(3)      NOT NULL CONSTRAINT DF_OrderItem_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_OrderItem PRIMARY KEY (Id),
    CONSTRAINT FK_OrderItem_SellerOrder_SellerOrderId FOREIGN KEY (SellerOrderId) REFERENCES dbo.SellerOrder (Id),
    CONSTRAINT FK_OrderItem_Offer_OfferId FOREIGN KEY (OfferId) REFERENCES dbo.Offer (Id),
    CONSTRAINT CK_OrderItem_Condition CHECK (Condition IN (N'New', N'Used')),
    CONSTRAINT CK_OrderItem_UnitPrice CHECK (UnitPrice > 0),
    CONSTRAINT CK_OrderItem_Quantity CHECK (Quantity BETWEEN 1 AND 10)
);
CREATE INDEX IX_OrderItem_SellerOrderId ON dbo.OrderItem (SellerOrderId);
GO

-- StockMovement: append-only inventory audit trail. Every stock change writes a row.
-- Restock/Adjustment change Stock; Reserve/Release change ReservedStock;
-- Sale decreases both (see business rule 5).
CREATE TABLE dbo.StockMovement
(
    Id                 int IDENTITY(1,1) NOT NULL,
    OfferId            int               NOT NULL,
    Reason             nvarchar(20)      NOT NULL,
    Change             int               NOT NULL,
    StockAfter         int               NOT NULL,
    ReservedStockAfter int               NOT NULL,
    SellerOrderId      int               NULL,      -- set for Reserve, Release and Sale
    CreatedByUserId    int               NULL,
    Note               nvarchar(300)     NULL,
    CreatedAt          datetime2(3)      NOT NULL CONSTRAINT DF_StockMovement_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_StockMovement PRIMARY KEY (Id),
    CONSTRAINT FK_StockMovement_Offer_OfferId FOREIGN KEY (OfferId) REFERENCES dbo.Offer (Id),
    CONSTRAINT FK_StockMovement_SellerOrder_SellerOrderId FOREIGN KEY (SellerOrderId) REFERENCES dbo.SellerOrder (Id),
    CONSTRAINT FK_StockMovement_AspNetUsers_CreatedByUserId FOREIGN KEY (CreatedByUserId) REFERENCES dbo.AspNetUsers (Id),
    CONSTRAINT CK_StockMovement_Reason CHECK (Reason IN (N'Restock', N'Adjustment', N'Reserve', N'Release', N'Sale')),
    CONSTRAINT CK_StockMovement_Change CHECK (Change <> 0)
);
CREATE INDEX IX_StockMovement_OfferId_CreatedAt ON dbo.StockMovement (OfferId, CreatedAt);
GO

/* =====================================================================
   Seed data (roles, brands, catalog) lives in Database/seed.sql.
   Run it after this script.
   ===================================================================== */

SET NOEXEC OFF;
GO

PRINT 'PcPartsMarketplace schema is ready.';
GO
