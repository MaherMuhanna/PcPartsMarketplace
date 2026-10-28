/* =====================================================================
   PC Parts Marketplace: seed data
   ---------------------------------------------------------------------
   - Run AFTER Database/schema.sql (SSMS: open and press F5, or
     sqlcmd -S localhost -E -C -i Database/seed.sql).
   - Idempotent: roles and brands are added only if missing; the catalog
     (categories, spec attributes, products) only if dbo.Category is empty.
   - Slugs follow the same rule as SlugGenerator in PCM.Infrastructure:
     lower case, every run of non a-z/0-9 characters becomes "-".
   - Demo accounts and the two demo seller proposals are created by the API
     at startup (Development only), because users need hashed passwords.
   ===================================================================== */

USE PcPartsMarketplace;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'dbo.Product', N'U') IS NULL
BEGIN
    RAISERROR('Tables not found. Run Database/schema.sql first.', 16, 1);
    SET NOEXEC ON;
END
GO

/* ---------------------------------------------------------------------
   1. Roles
   --------------------------------------------------------------------- */
INSERT INTO dbo.AspNetRoles (Name, NormalizedName, ConcurrencyStamp)
SELECT r.Name, UPPER(r.Name), CONVERT(nvarchar(36), NEWID())
FROM (VALUES (N'Admin'), (N'Seller'), (N'Customer')) AS r (Name)
WHERE NOT EXISTS (SELECT 1 FROM dbo.AspNetRoles x WHERE x.NormalizedName = UPPER(r.Name));
GO

/* ---------------------------------------------------------------------
   2. Brands
   --------------------------------------------------------------------- */
INSERT INTO dbo.Brand (Name, Slug)
SELECT b.Name, b.Slug
FROM (VALUES
    (N'AMD', 'amd'),
    (N'ASUS', 'asus'),
    (N'Corsair', 'corsair'),
    (N'G.Skill', 'g-skill'),
    (N'Intel', 'intel'),
    (N'Kingston', 'kingston'),
    (N'MSI', 'msi'),
    (N'NVIDIA', 'nvidia'),
    (N'Samsung', 'samsung'),
    (N'Western Digital', 'western-digital'),
    (N'ASRock', 'asrock'),
    (N'Cooler Master', 'cooler-master'),
    (N'Crucial', 'crucial'),
    (N'Gigabyte', 'gigabyte'),
    (N'Sapphire', 'sapphire'),
    (N'Seagate', 'seagate'),
    (N'Seasonic', 'seasonic')
) AS b (Name, Slug)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Brand x WHERE x.Name = b.Name);
GO

/* ---------------------------------------------------------------------
   3. Catalog (one transaction: all or nothing)
   --------------------------------------------------------------------- */
IF EXISTS (SELECT 1 FROM dbo.Category)
BEGIN
    PRINT 'Catalog already seeded. Skipping categories and products.';
    SET NOEXEC ON;
END
GO

SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- 3.1 Categories: one root, products attach to the leaves only.
INSERT INTO dbo.Category (ParentId, Name, Slug, SortOrder) VALUES (NULL, N'Components', 'components', 1);
DECLARE @RootId int = SCOPE_IDENTITY();

INSERT INTO dbo.Category (ParentId, Name, Slug, SortOrder)
VALUES
    (@RootId, N'CPU', 'cpu', 1),
    (@RootId, N'GPU', 'gpu', 2),
    (@RootId, N'RAM', 'ram', 3),
    (@RootId, N'Storage', 'storage', 4),
    (@RootId, N'Motherboard', 'motherboard', 5),
    (@RootId, N'Power Supply', 'power-supply', 6);
-- 3.2 Spec attributes per leaf category.
INSERT INTO dbo.SpecAttribute (CategoryId, Name, Slug, DataType, Unit, IsFilterable, IsRequired, SortOrder)
SELECT c.Id, v.Name, v.Slug, v.DataType, v.Unit, v.IsFilterable, v.IsRequired, v.SortOrder
FROM (VALUES
    ('cpu', N'Socket', 'socket', N'Enum', NULL, 1, 1, 1),
    ('cpu', N'Cores', 'cores', N'Number', NULL, 1, 1, 2),
    ('cpu', N'Threads', 'threads', N'Number', NULL, 0, 0, 3),
    ('cpu', N'Base Clock', 'base-clock', N'Number', N'GHz', 0, 0, 4),
    ('cpu', N'Boost Clock', 'boost-clock', N'Number', N'GHz', 1, 0, 5),
    ('cpu', N'TDP', 'tdp', N'Number', N'W', 1, 0, 6),
    ('cpu', N'Integrated Graphics', 'integrated-graphics', N'Enum', NULL, 1, 0, 7),
    ('gpu', N'Chipset', 'chipset', N'Enum', NULL, 1, 1, 1),
    ('gpu', N'Memory', 'memory', N'Number', N'GB', 1, 1, 2),
    ('gpu', N'Memory Type', 'memory-type', N'Enum', NULL, 1, 0, 3),
    ('gpu', N'Boost Clock', 'boost-clock', N'Number', N'MHz', 0, 0, 4),
    ('gpu', N'Recommended PSU', 'recommended-psu', N'Number', N'W', 1, 0, 5),
    ('gpu', N'Length', 'length', N'Number', N'mm', 0, 0, 6),
    ('ram', N'Memory Type', 'memory-type', N'Enum', NULL, 1, 1, 1),
    ('ram', N'Capacity', 'capacity', N'Number', N'GB', 1, 1, 2),
    ('ram', N'Speed', 'speed', N'Number', N'MT/s', 1, 1, 3),
    ('ram', N'Modules', 'modules', N'Enum', NULL, 1, 0, 4),
    ('ram', N'CAS Latency', 'cas-latency', N'Number', NULL, 1, 0, 5),
    ('ram', N'RGB', 'rgb', N'Enum', NULL, 1, 0, 6),
    ('storage', N'Type', 'type', N'Enum', NULL, 1, 1, 1),
    ('storage', N'Capacity', 'capacity', N'Number', N'GB', 1, 1, 2),
    ('storage', N'Interface', 'interface', N'Enum', NULL, 1, 0, 3),
    ('storage', N'Read Speed', 'read-speed', N'Number', N'MB/s', 1, 0, 4),
    ('storage', N'Write Speed', 'write-speed', N'Number', N'MB/s', 0, 0, 5),
    ('storage', N'Form Factor', 'form-factor', N'Enum', NULL, 1, 0, 6),
    ('motherboard', N'Socket', 'socket', N'Enum', NULL, 1, 1, 1),
    ('motherboard', N'Chipset', 'chipset', N'Enum', NULL, 1, 1, 2),
    ('motherboard', N'Form Factor', 'form-factor', N'Enum', NULL, 1, 1, 3),
    ('motherboard', N'Memory Type', 'memory-type', N'Enum', NULL, 1, 0, 4),
    ('motherboard', N'Memory Slots', 'memory-slots', N'Number', NULL, 0, 0, 5),
    ('motherboard', N'Wi-Fi', 'wi-fi', N'Enum', NULL, 1, 0, 6),
    ('power-supply', N'Wattage', 'wattage', N'Number', N'W', 1, 1, 1),
    ('power-supply', N'Efficiency', 'efficiency', N'Enum', NULL, 1, 1, 2),
    ('power-supply', N'Modular', 'modular', N'Enum', NULL, 1, 0, 3),
    ('power-supply', N'Form Factor', 'form-factor', N'Enum', NULL, 0, 0, 4)
) AS v (CategorySlug, Name, Slug, DataType, Unit, IsFilterable, IsRequired, SortOrder)
JOIN dbo.Category c ON c.Slug = v.CategorySlug;

-- 3.3 Allowed values of the Enum attributes.
INSERT INTO dbo.SpecOption (SpecAttributeId, Value, Slug, SortOrder)
SELECT sa.Id, v.Value, v.Slug, v.SortOrder
FROM (VALUES
    ('cpu', 'socket', N'AM5', 'am5', 1),
    ('cpu', 'socket', N'LGA1700', 'lga1700', 2),
    ('cpu', 'socket', N'LGA1851', 'lga1851', 3),
    ('cpu', 'integrated-graphics', N'Yes', 'yes', 1),
    ('cpu', 'integrated-graphics', N'No', 'no', 2),
    ('gpu', 'chipset', N'RTX 4060', 'rtx-4060', 1),
    ('gpu', 'chipset', N'RTX 4070 Super', 'rtx-4070-super', 2),
    ('gpu', 'chipset', N'RTX 4070 Ti Super', 'rtx-4070-ti-super', 3),
    ('gpu', 'chipset', N'RTX 5070', 'rtx-5070', 4),
    ('gpu', 'chipset', N'RX 7600', 'rx-7600', 5),
    ('gpu', 'chipset', N'RX 7800 XT', 'rx-7800-xt', 6),
    ('gpu', 'memory-type', N'GDDR6', 'gddr6', 1),
    ('gpu', 'memory-type', N'GDDR6X', 'gddr6x', 2),
    ('gpu', 'memory-type', N'GDDR7', 'gddr7', 3),
    ('ram', 'memory-type', N'DDR4', 'ddr4', 1),
    ('ram', 'memory-type', N'DDR5', 'ddr5', 2),
    ('ram', 'modules', N'1x16GB', '1x16gb', 1),
    ('ram', 'modules', N'2x8GB', '2x8gb', 2),
    ('ram', 'modules', N'2x16GB', '2x16gb', 3),
    ('ram', 'modules', N'2x32GB', '2x32gb', 4),
    ('ram', 'rgb', N'Yes', 'yes', 1),
    ('ram', 'rgb', N'No', 'no', 2),
    ('storage', 'type', N'NVMe SSD', 'nvme-ssd', 1),
    ('storage', 'type', N'SATA SSD', 'sata-ssd', 2),
    ('storage', 'type', N'HDD', 'hdd', 3),
    ('storage', 'interface', N'PCIe 3.0', 'pcie-3-0', 1),
    ('storage', 'interface', N'PCIe 4.0', 'pcie-4-0', 2),
    ('storage', 'interface', N'PCIe 5.0', 'pcie-5-0', 3),
    ('storage', 'interface', N'SATA III', 'sata-iii', 4),
    ('storage', 'form-factor', N'M.2 2280', 'm-2-2280', 1),
    ('storage', 'form-factor', N'2.5"', '2-5', 2),
    ('storage', 'form-factor', N'3.5"', '3-5', 3),
    ('motherboard', 'socket', N'AM5', 'am5', 1),
    ('motherboard', 'socket', N'LGA1700', 'lga1700', 2),
    ('motherboard', 'socket', N'LGA1851', 'lga1851', 3),
    ('motherboard', 'chipset', N'B650', 'b650', 1),
    ('motherboard', 'chipset', N'X670E', 'x670e', 2),
    ('motherboard', 'chipset', N'B760', 'b760', 3),
    ('motherboard', 'chipset', N'Z790', 'z790', 4),
    ('motherboard', 'form-factor', N'ATX', 'atx', 1),
    ('motherboard', 'form-factor', N'Micro-ATX', 'micro-atx', 2),
    ('motherboard', 'form-factor', N'Mini-ITX', 'mini-itx', 3),
    ('motherboard', 'memory-type', N'DDR4', 'ddr4', 1),
    ('motherboard', 'memory-type', N'DDR5', 'ddr5', 2),
    ('motherboard', 'wi-fi', N'Yes', 'yes', 1),
    ('motherboard', 'wi-fi', N'No', 'no', 2),
    ('power-supply', 'efficiency', N'80+ Bronze', '80-bronze', 1),
    ('power-supply', 'efficiency', N'80+ Gold', '80-gold', 2),
    ('power-supply', 'efficiency', N'80+ Platinum', '80-platinum', 3),
    ('power-supply', 'modular', N'Full', 'full', 1),
    ('power-supply', 'modular', N'Semi', 'semi', 2),
    ('power-supply', 'modular', N'Non', 'non', 3),
    ('power-supply', 'form-factor', N'ATX', 'atx', 1),
    ('power-supply', 'form-factor', N'SFX', 'sfx', 2)
) AS v (CategorySlug, AttributeSlug, Value, Slug, SortOrder)
JOIN dbo.Category c ON c.Slug = v.CategorySlug
JOIN dbo.SpecAttribute sa ON sa.CategoryId = c.Id AND sa.Slug = v.AttributeSlug;

-- 3.4 Products (admin-created, so Status = Approved and ProposedBySellerId = NULL).
INSERT INTO dbo.Product (CategoryId, BrandId, Name, ModelNumber, Slug, Description, Status, IsActive)
SELECT c.Id, b.Id, v.Name, v.ModelNumber, v.Slug, v.Description, N'Approved', v.IsActive
FROM (VALUES
    ('cpu', N'AMD', N'AMD Ryzen 7 7800X3D', '100-100000910WOF', 'amd-ryzen-7-7800x3d', N'8-core gaming processor with 3D V-Cache.', 1),
    ('cpu', N'AMD', N'AMD Ryzen 5 7600X', '100-100000593WOF', 'amd-ryzen-5-7600x', N'6-core processor for mainstream gaming builds.', 1),
    ('cpu', N'AMD', N'AMD Ryzen 9 7950X', '100-100000514WOF', 'amd-ryzen-9-7950x', N'16-core flagship for creators and heavy multitasking.', 1),
    ('cpu', N'Intel', N'Intel Core i5-14600KF', 'BX8071514600KF', 'intel-core-i5-14600kf', N'14-core unlocked processor without integrated graphics.', 1),
    ('cpu', N'Intel', N'Intel Core i7-14700K', 'BX8071514700K', 'intel-core-i7-14700k', N'20-core unlocked processor for gaming and streaming.', 1),
    ('cpu', N'Intel', N'Intel Core Ultra 7 265K', 'BX80768265K', 'intel-core-ultra-7-265k', N'20-core processor for the LGA1851 platform.', 1),
    ('cpu', N'Intel', N'Intel Core i9-13900K', 'BX8071513900K', 'intel-core-i9-13900k', N'Previous-generation flagship (inactive demo listing).', 0),
    ('gpu', N'ASUS', N'ASUS Dual GeForce RTX 4060 OC 8GB', 'DUAL-RTX4060-O8G', 'asus-dual-geforce-rtx-4060-oc-8gb', N'Compact dual-fan card for 1080p gaming.', 1),
    ('gpu', N'MSI', N'MSI GeForce RTX 4070 SUPER 12G VENTUS 2X OC', 'RTX 4070 SUPER 12G VENTUS 2X OC', 'msi-geforce-rtx-4070-super-12g-ventus-2x-oc', N'Dual-fan card for high-refresh 1440p gaming.', 1),
    ('gpu', N'Gigabyte', N'Gigabyte GeForce RTX 5070 WINDFORCE OC SFF 12G', 'GV-N5070WF3OC-12GD', 'gigabyte-geforce-rtx-5070-windforce-oc-sff-12g', N'Triple-fan RTX 50 series card.', 1),
    ('gpu', N'Sapphire', N'Sapphire PULSE Radeon RX 7600 8GB', '11324-01-20G', 'sapphire-pulse-radeon-rx-7600-8gb', N'Efficient card for 1080p gaming.', 1),
    ('gpu', N'Sapphire', N'Sapphire NITRO+ Radeon RX 7800 XT 16GB', '11330-01-20G', 'sapphire-nitro-radeon-rx-7800-xt-16gb', N'16 GB card for 1440p gaming.', 1),
    ('gpu', N'ASUS', N'ASUS TUF Gaming GeForce RTX 4070 Ti SUPER 16GB', 'TUF-RTX4070TIS-O16G-GAMING', 'asus-tuf-gaming-geforce-rtx-4070-ti-super-16gb', N'Durable triple-fan card with 16 GB memory.', 1),
    ('ram', N'Corsair', N'Corsair Vengeance DDR5 32GB (2x16GB) 6000MT/s CL30', 'CMK32GX5M2B6000C30', 'corsair-vengeance-ddr5-32gb-2x16gb-6000mt-s-cl30', N'Low-profile DDR5 kit tuned for AM5.', 1),
    ('ram', N'Corsair', N'Corsair Vengeance RGB DDR5 64GB (2x32GB) 6000MT/s CL30', 'CMH64GX5M2B6000C30', 'corsair-vengeance-rgb-ddr5-64gb-2x32gb-6000mt-s-cl30', N'64 GB RGB kit for creators.', 1),
    ('ram', N'G.Skill', N'G.Skill Trident Z5 RGB 32GB (2x16GB) 6400MT/s CL32', 'F5-6400J3239G16GX2-TZ5RK', 'g-skill-trident-z5-rgb-32gb-2x16gb-6400mt-s-cl32', N'High-speed RGB DDR5 kit.', 1),
    ('ram', N'Kingston', N'Kingston FURY Beast 16GB (2x8GB) DDR4 3200MT/s CL16', 'KF432C16BBK2/16', 'kingston-fury-beast-16gb-2x8gb-ddr4-3200mt-s-cl16', N'Reliable DDR4 kit for older platforms.', 1),
    ('ram', N'Crucial', N'Crucial Pro 32GB (2x16GB) DDR5 5600MT/s CL46', 'CP2K16G56C46U5', 'crucial-pro-32gb-2x16gb-ddr5-5600mt-s-cl46', N'Plug-and-play DDR5 kit.', 1),
    ('ram', N'Kingston', N'Kingston FURY Beast 16GB (1x16GB) DDR5 5200MT/s CL40', 'KF552C40BB-16', 'kingston-fury-beast-16gb-1x16gb-ddr5-5200mt-s-cl40', N'Single 16 GB DDR5 module.', 1),
    ('storage', N'Samsung', N'Samsung 990 PRO 2TB NVMe SSD', 'MZ-V9P2T0BW', 'samsung-990-pro-2tb-nvme-ssd', N'PCIe 4.0 NVMe SSD for fast loading.', 1),
    ('storage', N'Samsung', N'Samsung 870 EVO 1TB SATA SSD', 'MZ-77E1T0B/AM', 'samsung-870-evo-1tb-sata-ssd', N'2.5-inch SATA SSD upgrade.', 1),
    ('storage', N'Western Digital', N'WD_BLACK SN850X 1TB NVMe SSD', 'WDS100T2X0E', 'wd-black-sn850x-1tb-nvme-ssd', N'Gaming NVMe SSD.', 1),
    ('storage', N'Crucial', N'Crucial T705 2TB PCIe 5.0 NVMe SSD', 'CT2000T705SSD3', 'crucial-t705-2tb-pcie-5-0-nvme-ssd', N'PCIe 5.0 SSD for top transfer speeds.', 1),
    ('storage', N'Kingston', N'Kingston NV3 1TB NVMe SSD', 'SNV3S/1000G', 'kingston-nv3-1tb-nvme-ssd', N'Value PCIe 4.0 NVMe SSD.', 1),
    ('storage', N'Seagate', N'Seagate BarraCuda 4TB HDD', 'ST4000DM004', 'seagate-barracuda-4tb-hdd', N'3.5-inch hard drive for bulk storage.', 1),
    ('motherboard', N'MSI', N'MSI MAG B650 TOMAHAWK WIFI', 'MAG B650 TOMAHAWK WIFI', 'msi-mag-b650-tomahawk-wifi', N'ATX AM5 board with Wi-Fi 6E.', 1),
    ('motherboard', N'ASUS', N'ASUS ROG STRIX X670E-E GAMING WIFI', 'ROG STRIX X670E-E GAMING WIFI', 'asus-rog-strix-x670e-e-gaming-wifi', N'High-end AM5 board with PCIe 5.0.', 1),
    ('motherboard', N'Gigabyte', N'Gigabyte B650M AORUS ELITE AX', 'B650M AORUS ELITE AX', 'gigabyte-b650m-aorus-elite-ax', N'Micro-ATX AM5 board with Wi-Fi.', 1),
    ('motherboard', N'ASUS', N'ASUS TUF GAMING B760-PLUS WIFI D4', 'TUF GAMING B760-PLUS WIFI D4', 'asus-tuf-gaming-b760-plus-wifi-d4', N'LGA1700 board for DDR4 builds.', 1),
    ('motherboard', N'MSI', N'MSI MPG Z790 EDGE WIFI', 'MPG Z790 EDGE WIFI', 'msi-mpg-z790-edge-wifi', N'Z790 board for overclocking K processors.', 1),
    ('motherboard', N'ASRock', N'ASRock B760M Pro RS', 'B760M PRO RS', 'asrock-b760m-pro-rs', N'Budget Micro-ATX LGA1700 board.', 1),
    ('power-supply', N'Corsair', N'Corsair RM850e 850W 80+ Gold', 'CP-9020263-NA', 'corsair-rm850e-850w-80-gold', N'Fully modular ATX 3.0 power supply.', 1),
    ('power-supply', N'Seasonic', N'Seasonic FOCUS GX-750 750W 80+ Gold', 'SSR-750FX', 'seasonic-focus-gx-750-750w-80-gold', N'Fully modular power supply with a 10-year warranty.', 1),
    ('power-supply', N'Cooler Master', N'Cooler Master MWE Gold 650 V2 650W', 'MPE-6501-AFAAG', 'cooler-master-mwe-gold-650-v2-650w', N'Fully modular 650 W unit.', 1),
    ('power-supply', N'Corsair', N'Corsair CX650M 650W 80+ Bronze', 'CP-9020221-NA', 'corsair-cx650m-650w-80-bronze', N'Semi-modular budget power supply.', 1),
    ('power-supply', N'Seasonic', N'Seasonic PRIME PX-1000 1000W 80+ Platinum', 'SSR-1000PD', 'seasonic-prime-px-1000-1000w-80-platinum', N'Premium 1000 W unit for high-end builds.', 1),
    ('power-supply', N'Corsair', N'Corsair SF750 750W 80+ Platinum', 'CP-9020186-NA', 'corsair-sf750-750w-80-platinum', N'Small form factor power supply.', 1)
) AS v (CategorySlug, BrandName, Name, ModelNumber, Slug, Description, IsActive)
JOIN dbo.Category c ON c.Slug = v.CategorySlug
JOIN dbo.Brand b ON b.Name = v.BrandName;

-- 3.5 One image per product: a generic picture of its category, served by PCM.UI.
INSERT INTO dbo.ProductImage (ProductId, Url, AltText, SortOrder)
SELECT p.Id, CONCAT(N'/images/products/', c.Slug, N'.svg'), p.Name, 0
FROM dbo.Product p
JOIN dbo.Category c ON c.Id = p.CategoryId;

-- 3.6 Spec values. Number attributes fill NumberValue, Enum attributes SpecOptionId.
INSERT INTO dbo.ProductSpecValue (ProductId, SpecAttributeId, NumberValue, SpecOptionId)
SELECT p.Id, sa.Id, v.NumberValue, so.Id
FROM (VALUES
    ('100-100000910WOF', 'socket', NULL, 'am5'),
    ('100-100000910WOF', 'cores', 8, NULL),
    ('100-100000910WOF', 'threads', 16, NULL),
    ('100-100000910WOF', 'base-clock', 4.2, NULL),
    ('100-100000910WOF', 'boost-clock', 5.0, NULL),
    ('100-100000910WOF', 'tdp', 120, NULL),
    ('100-100000910WOF', 'integrated-graphics', NULL, 'yes'),
    ('100-100000593WOF', 'socket', NULL, 'am5'),
    ('100-100000593WOF', 'cores', 6, NULL),
    ('100-100000593WOF', 'threads', 12, NULL),
    ('100-100000593WOF', 'base-clock', 4.7, NULL),
    ('100-100000593WOF', 'boost-clock', 5.3, NULL),
    ('100-100000593WOF', 'tdp', 105, NULL),
    ('100-100000593WOF', 'integrated-graphics', NULL, 'yes'),
    ('100-100000514WOF', 'socket', NULL, 'am5'),
    ('100-100000514WOF', 'cores', 16, NULL),
    ('100-100000514WOF', 'threads', 32, NULL),
    ('100-100000514WOF', 'base-clock', 4.5, NULL),
    ('100-100000514WOF', 'boost-clock', 5.7, NULL),
    ('100-100000514WOF', 'tdp', 170, NULL),
    ('100-100000514WOF', 'integrated-graphics', NULL, 'yes'),
    ('BX8071514600KF', 'socket', NULL, 'lga1700'),
    ('BX8071514600KF', 'cores', 14, NULL),
    ('BX8071514600KF', 'threads', 20, NULL),
    ('BX8071514600KF', 'base-clock', 3.5, NULL),
    ('BX8071514600KF', 'boost-clock', 5.3, NULL),
    ('BX8071514600KF', 'tdp', 125, NULL),
    ('BX8071514600KF', 'integrated-graphics', NULL, 'no'),
    ('BX8071514700K', 'socket', NULL, 'lga1700'),
    ('BX8071514700K', 'cores', 20, NULL),
    ('BX8071514700K', 'threads', 28, NULL),
    ('BX8071514700K', 'base-clock', 3.4, NULL),
    ('BX8071514700K', 'boost-clock', 5.6, NULL),
    ('BX8071514700K', 'tdp', 125, NULL),
    ('BX8071514700K', 'integrated-graphics', NULL, 'yes'),
    ('BX80768265K', 'socket', NULL, 'lga1851'),
    ('BX80768265K', 'cores', 20, NULL),
    ('BX80768265K', 'threads', 20, NULL),
    ('BX80768265K', 'base-clock', 3.9, NULL),
    ('BX80768265K', 'boost-clock', 5.5, NULL),
    ('BX80768265K', 'tdp', 125, NULL),
    ('BX80768265K', 'integrated-graphics', NULL, 'yes'),
    ('BX8071513900K', 'socket', NULL, 'lga1700'),
    ('BX8071513900K', 'cores', 24, NULL),
    ('BX8071513900K', 'threads', 32, NULL),
    ('BX8071513900K', 'base-clock', 3.0, NULL),
    ('BX8071513900K', 'boost-clock', 5.8, NULL),
    ('BX8071513900K', 'tdp', 125, NULL),
    ('BX8071513900K', 'integrated-graphics', NULL, 'yes'),
    ('DUAL-RTX4060-O8G', 'chipset', NULL, 'rtx-4060'),
    ('DUAL-RTX4060-O8G', 'memory', 8, NULL),
    ('DUAL-RTX4060-O8G', 'memory-type', NULL, 'gddr6'),
    ('DUAL-RTX4060-O8G', 'boost-clock', 2535, NULL),
    ('DUAL-RTX4060-O8G', 'recommended-psu', 550, NULL),
    ('DUAL-RTX4060-O8G', 'length', 227, NULL),
    ('RTX 4070 SUPER 12G VENTUS 2X OC', 'chipset', NULL, 'rtx-4070-super'),
    ('RTX 4070 SUPER 12G VENTUS 2X OC', 'memory', 12, NULL),
    ('RTX 4070 SUPER 12G VENTUS 2X OC', 'memory-type', NULL, 'gddr6x'),
    ('RTX 4070 SUPER 12G VENTUS 2X OC', 'boost-clock', 2505, NULL),
    ('RTX 4070 SUPER 12G VENTUS 2X OC', 'recommended-psu', 650, NULL),
    ('RTX 4070 SUPER 12G VENTUS 2X OC', 'length', 242, NULL),
    ('GV-N5070WF3OC-12GD', 'chipset', NULL, 'rtx-5070'),
    ('GV-N5070WF3OC-12GD', 'memory', 12, NULL),
    ('GV-N5070WF3OC-12GD', 'memory-type', NULL, 'gddr7'),
    ('GV-N5070WF3OC-12GD', 'boost-clock', 2557, NULL),
    ('GV-N5070WF3OC-12GD', 'recommended-psu', 650, NULL),
    ('GV-N5070WF3OC-12GD', 'length', 304, NULL),
    ('11324-01-20G', 'chipset', NULL, 'rx-7600'),
    ('11324-01-20G', 'memory', 8, NULL),
    ('11324-01-20G', 'memory-type', NULL, 'gddr6'),
    ('11324-01-20G', 'boost-clock', 2655, NULL),
    ('11324-01-20G', 'recommended-psu', 550, NULL),
    ('11324-01-20G', 'length', 240, NULL),
    ('11330-01-20G', 'chipset', NULL, 'rx-7800-xt'),
    ('11330-01-20G', 'memory', 16, NULL),
    ('11330-01-20G', 'memory-type', NULL, 'gddr6'),
    ('11330-01-20G', 'boost-clock', 2565, NULL),
    ('11330-01-20G', 'recommended-psu', 700, NULL),
    ('11330-01-20G', 'length', 320, NULL),
    ('TUF-RTX4070TIS-O16G-GAMING', 'chipset', NULL, 'rtx-4070-ti-super'),
    ('TUF-RTX4070TIS-O16G-GAMING', 'memory', 16, NULL),
    ('TUF-RTX4070TIS-O16G-GAMING', 'memory-type', NULL, 'gddr6x'),
    ('TUF-RTX4070TIS-O16G-GAMING', 'boost-clock', 2640, NULL),
    ('TUF-RTX4070TIS-O16G-GAMING', 'recommended-psu', 750, NULL),
    ('TUF-RTX4070TIS-O16G-GAMING', 'length', 305, NULL),
    ('CMK32GX5M2B6000C30', 'memory-type', NULL, 'ddr5'),
    ('CMK32GX5M2B6000C30', 'capacity', 32, NULL),
    ('CMK32GX5M2B6000C30', 'speed', 6000, NULL),
    ('CMK32GX5M2B6000C30', 'modules', NULL, '2x16gb'),
    ('CMK32GX5M2B6000C30', 'cas-latency', 30, NULL),
    ('CMK32GX5M2B6000C30', 'rgb', NULL, 'no'),
    ('CMH64GX5M2B6000C30', 'memory-type', NULL, 'ddr5'),
    ('CMH64GX5M2B6000C30', 'capacity', 64, NULL),
    ('CMH64GX5M2B6000C30', 'speed', 6000, NULL),
    ('CMH64GX5M2B6000C30', 'modules', NULL, '2x32gb'),
    ('CMH64GX5M2B6000C30', 'cas-latency', 30, NULL),
    ('CMH64GX5M2B6000C30', 'rgb', NULL, 'yes'),
    ('F5-6400J3239G16GX2-TZ5RK', 'memory-type', NULL, 'ddr5'),
    ('F5-6400J3239G16GX2-TZ5RK', 'capacity', 32, NULL),
    ('F5-6400J3239G16GX2-TZ5RK', 'speed', 6400, NULL),
    ('F5-6400J3239G16GX2-TZ5RK', 'modules', NULL, '2x16gb'),
    ('F5-6400J3239G16GX2-TZ5RK', 'cas-latency', 32, NULL),
    ('F5-6400J3239G16GX2-TZ5RK', 'rgb', NULL, 'yes'),
    ('KF432C16BBK2/16', 'memory-type', NULL, 'ddr4'),
    ('KF432C16BBK2/16', 'capacity', 16, NULL),
    ('KF432C16BBK2/16', 'speed', 3200, NULL),
    ('KF432C16BBK2/16', 'modules', NULL, '2x8gb'),
    ('KF432C16BBK2/16', 'cas-latency', 16, NULL),
    ('KF432C16BBK2/16', 'rgb', NULL, 'no'),
    ('CP2K16G56C46U5', 'memory-type', NULL, 'ddr5'),
    ('CP2K16G56C46U5', 'capacity', 32, NULL),
    ('CP2K16G56C46U5', 'speed', 5600, NULL),
    ('CP2K16G56C46U5', 'modules', NULL, '2x16gb'),
    ('CP2K16G56C46U5', 'cas-latency', 46, NULL),
    ('CP2K16G56C46U5', 'rgb', NULL, 'no'),
    ('KF552C40BB-16', 'memory-type', NULL, 'ddr5'),
    ('KF552C40BB-16', 'capacity', 16, NULL),
    ('KF552C40BB-16', 'speed', 5200, NULL),
    ('KF552C40BB-16', 'modules', NULL, '1x16gb'),
    ('KF552C40BB-16', 'cas-latency', 40, NULL),
    ('KF552C40BB-16', 'rgb', NULL, 'no'),
    ('MZ-V9P2T0BW', 'type', NULL, 'nvme-ssd'),
    ('MZ-V9P2T0BW', 'capacity', 2000, NULL),
    ('MZ-V9P2T0BW', 'interface', NULL, 'pcie-4-0'),
    ('MZ-V9P2T0BW', 'read-speed', 7450, NULL),
    ('MZ-V9P2T0BW', 'write-speed', 6900, NULL),
    ('MZ-V9P2T0BW', 'form-factor', NULL, 'm-2-2280'),
    ('MZ-77E1T0B/AM', 'type', NULL, 'sata-ssd'),
    ('MZ-77E1T0B/AM', 'capacity', 1000, NULL),
    ('MZ-77E1T0B/AM', 'interface', NULL, 'sata-iii'),
    ('MZ-77E1T0B/AM', 'read-speed', 560, NULL),
    ('MZ-77E1T0B/AM', 'write-speed', 530, NULL),
    ('MZ-77E1T0B/AM', 'form-factor', NULL, '2-5'),
    ('WDS100T2X0E', 'type', NULL, 'nvme-ssd'),
    ('WDS100T2X0E', 'capacity', 1000, NULL),
    ('WDS100T2X0E', 'interface', NULL, 'pcie-4-0'),
    ('WDS100T2X0E', 'read-speed', 7300, NULL),
    ('WDS100T2X0E', 'write-speed', 6300, NULL),
    ('WDS100T2X0E', 'form-factor', NULL, 'm-2-2280'),
    ('CT2000T705SSD3', 'type', NULL, 'nvme-ssd'),
    ('CT2000T705SSD3', 'capacity', 2000, NULL),
    ('CT2000T705SSD3', 'interface', NULL, 'pcie-5-0'),
    ('CT2000T705SSD3', 'read-speed', 14500, NULL),
    ('CT2000T705SSD3', 'write-speed', 12700, NULL),
    ('CT2000T705SSD3', 'form-factor', NULL, 'm-2-2280'),
    ('SNV3S/1000G', 'type', NULL, 'nvme-ssd'),
    ('SNV3S/1000G', 'capacity', 1000, NULL),
    ('SNV3S/1000G', 'interface', NULL, 'pcie-4-0'),
    ('SNV3S/1000G', 'read-speed', 6000, NULL),
    ('SNV3S/1000G', 'write-speed', 4000, NULL),
    ('SNV3S/1000G', 'form-factor', NULL, 'm-2-2280'),
    ('ST4000DM004', 'type', NULL, 'hdd'),
    ('ST4000DM004', 'capacity', 4000, NULL),
    ('ST4000DM004', 'interface', NULL, 'sata-iii'),
    ('ST4000DM004', 'read-speed', 190, NULL),
    ('ST4000DM004', 'write-speed', 190, NULL),
    ('ST4000DM004', 'form-factor', NULL, '3-5'),
    ('MAG B650 TOMAHAWK WIFI', 'socket', NULL, 'am5'),
    ('MAG B650 TOMAHAWK WIFI', 'chipset', NULL, 'b650'),
    ('MAG B650 TOMAHAWK WIFI', 'form-factor', NULL, 'atx'),
    ('MAG B650 TOMAHAWK WIFI', 'memory-type', NULL, 'ddr5'),
    ('MAG B650 TOMAHAWK WIFI', 'memory-slots', 4, NULL),
    ('MAG B650 TOMAHAWK WIFI', 'wi-fi', NULL, 'yes'),
    ('ROG STRIX X670E-E GAMING WIFI', 'socket', NULL, 'am5'),
    ('ROG STRIX X670E-E GAMING WIFI', 'chipset', NULL, 'x670e'),
    ('ROG STRIX X670E-E GAMING WIFI', 'form-factor', NULL, 'atx'),
    ('ROG STRIX X670E-E GAMING WIFI', 'memory-type', NULL, 'ddr5'),
    ('ROG STRIX X670E-E GAMING WIFI', 'memory-slots', 4, NULL),
    ('ROG STRIX X670E-E GAMING WIFI', 'wi-fi', NULL, 'yes'),
    ('B650M AORUS ELITE AX', 'socket', NULL, 'am5'),
    ('B650M AORUS ELITE AX', 'chipset', NULL, 'b650'),
    ('B650M AORUS ELITE AX', 'form-factor', NULL, 'micro-atx'),
    ('B650M AORUS ELITE AX', 'memory-type', NULL, 'ddr5'),
    ('B650M AORUS ELITE AX', 'memory-slots', 4, NULL),
    ('B650M AORUS ELITE AX', 'wi-fi', NULL, 'yes'),
    ('TUF GAMING B760-PLUS WIFI D4', 'socket', NULL, 'lga1700'),
    ('TUF GAMING B760-PLUS WIFI D4', 'chipset', NULL, 'b760'),
    ('TUF GAMING B760-PLUS WIFI D4', 'form-factor', NULL, 'atx'),
    ('TUF GAMING B760-PLUS WIFI D4', 'memory-type', NULL, 'ddr4'),
    ('TUF GAMING B760-PLUS WIFI D4', 'memory-slots', 4, NULL),
    ('TUF GAMING B760-PLUS WIFI D4', 'wi-fi', NULL, 'yes'),
    ('MPG Z790 EDGE WIFI', 'socket', NULL, 'lga1700'),
    ('MPG Z790 EDGE WIFI', 'chipset', NULL, 'z790'),
    ('MPG Z790 EDGE WIFI', 'form-factor', NULL, 'atx'),
    ('MPG Z790 EDGE WIFI', 'memory-type', NULL, 'ddr5'),
    ('MPG Z790 EDGE WIFI', 'memory-slots', 4, NULL),
    ('MPG Z790 EDGE WIFI', 'wi-fi', NULL, 'yes'),
    ('B760M PRO RS', 'socket', NULL, 'lga1700'),
    ('B760M PRO RS', 'chipset', NULL, 'b760'),
    ('B760M PRO RS', 'form-factor', NULL, 'micro-atx'),
    ('B760M PRO RS', 'memory-type', NULL, 'ddr5'),
    ('B760M PRO RS', 'memory-slots', 4, NULL),
    ('B760M PRO RS', 'wi-fi', NULL, 'no'),
    ('CP-9020263-NA', 'wattage', 850, NULL),
    ('CP-9020263-NA', 'efficiency', NULL, '80-gold'),
    ('CP-9020263-NA', 'modular', NULL, 'full'),
    ('CP-9020263-NA', 'form-factor', NULL, 'atx'),
    ('SSR-750FX', 'wattage', 750, NULL),
    ('SSR-750FX', 'efficiency', NULL, '80-gold'),
    ('SSR-750FX', 'modular', NULL, 'full'),
    ('SSR-750FX', 'form-factor', NULL, 'atx'),
    ('MPE-6501-AFAAG', 'wattage', 650, NULL),
    ('MPE-6501-AFAAG', 'efficiency', NULL, '80-gold'),
    ('MPE-6501-AFAAG', 'modular', NULL, 'full'),
    ('MPE-6501-AFAAG', 'form-factor', NULL, 'atx'),
    ('CP-9020221-NA', 'wattage', 650, NULL),
    ('CP-9020221-NA', 'efficiency', NULL, '80-bronze'),
    ('CP-9020221-NA', 'modular', NULL, 'semi'),
    ('CP-9020221-NA', 'form-factor', NULL, 'atx'),
    ('SSR-1000PD', 'wattage', 1000, NULL),
    ('SSR-1000PD', 'efficiency', NULL, '80-platinum'),
    ('SSR-1000PD', 'modular', NULL, 'full'),
    ('SSR-1000PD', 'form-factor', NULL, 'atx'),
    ('CP-9020186-NA', 'wattage', 750, NULL),
    ('CP-9020186-NA', 'efficiency', NULL, '80-platinum'),
    ('CP-9020186-NA', 'modular', NULL, 'full'),
    ('CP-9020186-NA', 'form-factor', NULL, 'sfx')
) AS v (ModelNumber, AttributeSlug, NumberValue, OptionSlug)
JOIN dbo.Product p ON p.ModelNumber = v.ModelNumber
JOIN dbo.SpecAttribute sa ON sa.CategoryId = p.CategoryId AND sa.Slug = v.AttributeSlug
LEFT JOIN dbo.SpecOption so ON so.SpecAttributeId = sa.Id AND so.Slug = v.OptionSlug;

-- Safety net: every Enum row must have found its option.
IF EXISTS (SELECT 1 FROM dbo.ProductSpecValue WHERE NumberValue IS NULL AND SpecOptionId IS NULL AND TextValue IS NULL)
    THROW 50000, 'Seed error: a spec value did not match any option.', 1;

COMMIT TRANSACTION;
GO

SET NOEXEC OFF;
GO

-- Only claim success when the seed actually ran against the right database.
IF DB_NAME() = N'PcPartsMarketplace' AND OBJECT_ID(N'dbo.Product', N'U') IS NOT NULL
    PRINT 'PcPartsMarketplace seed data is ready.';
ELSE
    PRINT 'Seed did NOT run: run Database/schema.sql first, then this script again.';
GO
