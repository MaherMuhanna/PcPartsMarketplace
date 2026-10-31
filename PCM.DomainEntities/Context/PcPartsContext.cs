using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using PCM.DomainEntities.DBEntities;

namespace PCM.DomainEntities.Context;

public partial class PcPartsContext : DbContext
{
    public PcPartsContext(DbContextOptions<PcPartsContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Address> Addresses { get; set; }

    public virtual DbSet<Brand> Brands { get; set; }

    public virtual DbSet<Cart> Carts { get; set; }

    public virtual DbSet<CartItem> CartItems { get; set; }

    public virtual DbSet<Category> Categories { get; set; }

    public virtual DbSet<Offer> Offers { get; set; }

    public virtual DbSet<Order> Orders { get; set; }

    public virtual DbSet<OrderItem> OrderItems { get; set; }

    public virtual DbSet<Product> Products { get; set; }

    public virtual DbSet<ProductImage> ProductImages { get; set; }

    public virtual DbSet<ProductSpecValue> ProductSpecValues { get; set; }

    public virtual DbSet<RefreshToken> RefreshTokens { get; set; }

    public virtual DbSet<SellerOrder> SellerOrders { get; set; }

    public virtual DbSet<SellerProfile> SellerProfiles { get; set; }

    public virtual DbSet<SpecAttribute> SpecAttributes { get; set; }

    public virtual DbSet<SpecOption> SpecOptions { get; set; }

    public virtual DbSet<StockMovement> StockMovements { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Address>(entity =>
        {
            entity.ToTable("Address");

            entity.HasIndex(e => e.UserId, "IX_Address_UserId");

            entity.HasIndex(e => e.UserId, "UX_Address_UserId_Default")
                .IsUnique()
                .HasFilter("([IsDefault]=(1) AND [IsDeleted]=(0))");

            entity.Property(e => e.City).HasMaxLength(100);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_Address_CreatedAt");
            entity.Property(e => e.Details).HasMaxLength(200);
            entity.Property(e => e.Phone)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Street).HasMaxLength(200);
            entity.Property(e => e.UpdatedAt).HasPrecision(3);
        });

        modelBuilder.Entity<Brand>(entity =>
        {
            entity.ToTable("Brand");

            entity.HasIndex(e => e.Name, "UX_Brand_Name")
                .IsUnique()
                .HasFilter("([IsDeleted]=(0))");

            entity.HasIndex(e => e.Slug, "UX_Brand_Slug")
                .IsUnique()
                .HasFilter("([IsDeleted]=(0))");

            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_Brand_CreatedAt");
            entity.Property(e => e.LogoUrl).HasMaxLength(500);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.Slug)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.UpdatedAt).HasPrecision(3);
        });

        modelBuilder.Entity<Cart>(entity =>
        {
            entity.ToTable("Cart");

            entity.HasIndex(e => e.CustomerId, "UX_Cart_CustomerId").IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_Cart_CreatedAt");
            entity.Property(e => e.UpdatedAt).HasPrecision(3);
        });

        modelBuilder.Entity<CartItem>(entity =>
        {
            entity.ToTable("CartItem");

            entity.HasIndex(e => new { e.CartId, e.OfferId }, "UX_CartItem_CartId_OfferId").IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_CartItem_CreatedAt");
            entity.Property(e => e.UpdatedAt).HasPrecision(3);

            entity.HasOne(d => d.Cart).WithMany(p => p.CartItems).HasForeignKey(d => d.CartId);

            entity.HasOne(d => d.Offer).WithMany(p => p.CartItems)
                .HasForeignKey(d => d.OfferId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.ToTable("Category");

            entity.HasIndex(e => e.ParentId, "IX_Category_ParentId");

            entity.HasIndex(e => new { e.ParentId, e.Name }, "UX_Category_ParentId_Name")
                .IsUnique()
                .HasFilter("([IsDeleted]=(0))");

            entity.HasIndex(e => e.Slug, "UX_Category_Slug")
                .IsUnique()
                .HasFilter("([IsDeleted]=(0))");

            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_Category_CreatedAt");
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.Slug)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.UpdatedAt).HasPrecision(3);

            entity.HasOne(d => d.Parent).WithMany(p => p.InverseParent).HasForeignKey(d => d.ParentId);
        });

        modelBuilder.Entity<Offer>(entity =>
        {
            entity.ToTable("Offer");

            entity.HasIndex(e => new { e.ProductId, e.Price }, "IX_Offer_ProductId_Price");

            entity.HasIndex(e => e.SellerId, "IX_Offer_SellerId");

            entity.HasIndex(e => new { e.ProductId, e.SellerId, e.Condition }, "UX_Offer_Product_Seller_Condition")
                .IsUnique()
                .HasFilter("([IsDeleted]=(0))");

            entity.Property(e => e.Condition)
                .HasMaxLength(20)
                .HasDefaultValue("New", "DF_Offer_Condition");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_Offer_CreatedAt");
            entity.Property(e => e.LowStockThreshold).HasDefaultValue(5, "DF_Offer_LowStockThreshold");
            entity.Property(e => e.Price).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.UpdatedAt).HasPrecision(3);

            entity.HasOne(d => d.Product).WithMany(p => p.Offers)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.Seller).WithMany(p => p.Offers)
                .HasForeignKey(d => d.SellerId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("Order");

            entity.HasIndex(e => new { e.CustomerId, e.CreatedAt }, "IX_Order_CustomerId_CreatedAt");

            entity.HasIndex(e => e.OrderNumber, "UX_Order_OrderNumber").IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_Order_CreatedAt");
            entity.Property(e => e.OrderNumber)
                .HasMaxLength(20)
                .HasComputedColumnSql("(CONVERT([nvarchar](20),N'PCM-'+case when [Id]<(1000000) then right(N'000000'+CONVERT([nvarchar](10),[Id]),(6)) else CONVERT([nvarchar](10),[Id]) end))", true);
            entity.Property(e => e.PaymentMethod)
                .HasMaxLength(20)
                .HasDefaultValue("COD", "DF_Order_PaymentMethod");
            entity.Property(e => e.ShipCity).HasMaxLength(100);
            entity.Property(e => e.ShipDetails).HasMaxLength(200);
            entity.Property(e => e.ShipPhone)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.ShipRecipientName).HasMaxLength(100);
            entity.Property(e => e.ShipStreet).HasMaxLength(200);
            entity.Property(e => e.Total).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.UpdatedAt).HasPrecision(3);

            entity.HasOne(d => d.ShippingAddress).WithMany(p => p.Orders)
                .HasForeignKey(d => d.ShippingAddressId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.ToTable("OrderItem");

            entity.HasIndex(e => e.SellerOrderId, "IX_OrderItem_SellerOrderId");

            entity.Property(e => e.Condition).HasMaxLength(20);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_OrderItem_CreatedAt");
            entity.Property(e => e.LineTotal)
                .HasComputedColumnSql("([UnitPrice]*[Quantity])", true)
                .HasColumnType("decimal(29, 2)");
            entity.Property(e => e.ProductName).HasMaxLength(200);
            entity.Property(e => e.UnitPrice).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Offer).WithMany(p => p.OrderItems)
                .HasForeignKey(d => d.OfferId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.SellerOrder).WithMany(p => p.OrderItems)
                .HasForeignKey(d => d.SellerOrderId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("Product");

            entity.HasIndex(e => new { e.CategoryId, e.BrandId }, "IX_Product_CategoryId_BrandId");

            entity.HasIndex(e => e.Status, "IX_Product_Status");

            entity.HasIndex(e => new { e.BrandId, e.ModelNumber }, "UX_Product_BrandId_ModelNumber")
                .IsUnique()
                .HasFilter("([IsDeleted]=(0))");

            entity.HasIndex(e => e.Slug, "UX_Product_Slug")
                .IsUnique()
                .HasFilter("([IsDeleted]=(0))");

            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_Product_CreatedAt");
            entity.Property(e => e.IsActive).HasDefaultValue(true, "DF_Product_IsActive");
            entity.Property(e => e.ModelNumber)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.ReviewNote).HasMaxLength(500);
            entity.Property(e => e.Slug)
                .HasMaxLength(250)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("Approved", "DF_Product_Status");
            entity.Property(e => e.UpdatedAt).HasPrecision(3);

            entity.HasOne(d => d.Brand).WithMany(p => p.Products)
                .HasForeignKey(d => d.BrandId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.Category).WithMany(p => p.Products)
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.ProposedBySeller).WithMany(p => p.Products).HasForeignKey(d => d.ProposedBySellerId);
        });

        modelBuilder.Entity<ProductImage>(entity =>
        {
            entity.ToTable("ProductImage");

            entity.HasIndex(e => new { e.ProductId, e.SortOrder }, "IX_ProductImage_ProductId_SortOrder");

            entity.Property(e => e.AltText).HasMaxLength(200);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_ProductImage_CreatedAt");
            entity.Property(e => e.Url).HasMaxLength(500);

            entity.HasOne(d => d.Product).WithMany(p => p.ProductImages).HasForeignKey(d => d.ProductId);
        });

        modelBuilder.Entity<ProductSpecValue>(entity =>
        {
            entity.HasKey(e => new { e.ProductId, e.SpecAttributeId });

            entity.ToTable("ProductSpecValue");

            entity.HasIndex(e => new { e.SpecAttributeId, e.NumberValue }, "IX_ProductSpecValue_Attr_Number");

            entity.HasIndex(e => new { e.SpecAttributeId, e.SpecOptionId }, "IX_ProductSpecValue_Attr_Option");

            entity.Property(e => e.NumberValue).HasColumnType("decimal(18, 4)");
            entity.Property(e => e.TextValue).HasMaxLength(200);

            entity.HasOne(d => d.Product).WithMany(p => p.ProductSpecValues).HasForeignKey(d => d.ProductId);

            entity.HasOne(d => d.SpecAttribute).WithMany(p => p.ProductSpecValues)
                .HasForeignKey(d => d.SpecAttributeId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.SpecOption).WithMany(p => p.ProductSpecValues).HasForeignKey(d => d.SpecOptionId);
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("RefreshToken");

            entity.HasIndex(e => e.UserId, "IX_RefreshToken_UserId");

            entity.HasIndex(e => e.TokenHash, "UX_RefreshToken_TokenHash").IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_RefreshToken_CreatedAt");
            entity.Property(e => e.CreatedByIp)
                .HasMaxLength(45)
                .IsUnicode(false);
            entity.Property(e => e.ExpiresAt).HasPrecision(3);
            entity.Property(e => e.RevokedAt).HasPrecision(3);
            entity.Property(e => e.TokenHash)
                .HasMaxLength(64)
                .IsUnicode(false)
                .IsFixedLength();

            entity.HasOne(d => d.ReplacedByToken).WithMany(p => p.InverseReplacedByToken).HasForeignKey(d => d.ReplacedByTokenId);
        });

        modelBuilder.Entity<SellerOrder>(entity =>
        {
            entity.ToTable("SellerOrder");

            entity.HasIndex(e => new { e.SellerId, e.Status }, "IX_SellerOrder_SellerId_Status");

            entity.HasIndex(e => new { e.Status, e.CreatedAt }, "IX_SellerOrder_Status_CreatedAt");

            entity.HasIndex(e => new { e.OrderId, e.SellerId }, "UX_SellerOrder_OrderId_SellerId").IsUnique();

            entity.Property(e => e.CancelReason).HasMaxLength(300);
            entity.Property(e => e.CancelledAt).HasPrecision(3);
            entity.Property(e => e.ConfirmedAt).HasPrecision(3);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_SellerOrder_CreatedAt");
            entity.Property(e => e.DeliveredAt).HasPrecision(3);
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.ShippedAt).HasPrecision(3);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("Pending", "DF_SellerOrder_Status");
            entity.Property(e => e.Subtotal).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.UpdatedAt).HasPrecision(3);

            entity.HasOne(d => d.Order).WithMany(p => p.SellerOrders)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.Seller).WithMany(p => p.SellerOrders)
                .HasForeignKey(d => d.SellerId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<SellerProfile>(entity =>
        {
            entity.ToTable("SellerProfile");

            entity.HasIndex(e => e.Status, "IX_SellerProfile_Status");

            entity.HasIndex(e => e.StoreName, "UX_SellerProfile_StoreName")
                .IsUnique()
                .HasFilter("([IsDeleted]=(0))");

            entity.HasIndex(e => e.UserId, "UX_SellerProfile_UserId").IsUnique();

            entity.Property(e => e.ApprovedAt).HasPrecision(3);
            entity.Property(e => e.ContactPhone)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_SellerProfile_CreatedAt");
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("Pending", "DF_SellerProfile_Status");
            entity.Property(e => e.StatusReason).HasMaxLength(500);
            entity.Property(e => e.StoreName).HasMaxLength(100);
            entity.Property(e => e.UpdatedAt).HasPrecision(3);
        });

        modelBuilder.Entity<SpecAttribute>(entity =>
        {
            entity.ToTable("SpecAttribute");

            entity.HasIndex(e => new { e.CategoryId, e.Name }, "UX_SpecAttribute_CategoryId_Name")
                .IsUnique()
                .HasFilter("([IsDeleted]=(0))");

            entity.HasIndex(e => new { e.CategoryId, e.Slug }, "UX_SpecAttribute_CategoryId_Slug")
                .IsUnique()
                .HasFilter("([IsDeleted]=(0))");

            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_SpecAttribute_CreatedAt");
            entity.Property(e => e.DataType).HasMaxLength(20);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.Slug)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Unit).HasMaxLength(20);
            entity.Property(e => e.UpdatedAt).HasPrecision(3);

            entity.HasOne(d => d.Category).WithMany(p => p.SpecAttributes)
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<SpecOption>(entity =>
        {
            entity.ToTable("SpecOption");

            entity.HasIndex(e => new { e.SpecAttributeId, e.Slug }, "UX_SpecOption_SpecAttributeId_Slug")
                .IsUnique()
                .HasFilter("([IsDeleted]=(0))");

            entity.HasIndex(e => new { e.SpecAttributeId, e.Value }, "UX_SpecOption_SpecAttributeId_Value")
                .IsUnique()
                .HasFilter("([IsDeleted]=(0))");

            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_SpecOption_CreatedAt");
            entity.Property(e => e.Slug)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.UpdatedAt).HasPrecision(3);
            entity.Property(e => e.Value).HasMaxLength(100);

            entity.HasOne(d => d.SpecAttribute).WithMany(p => p.SpecOptions)
                .HasForeignKey(d => d.SpecAttributeId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<StockMovement>(entity =>
        {
            entity.ToTable("StockMovement");

            entity.HasIndex(e => new { e.OfferId, e.CreatedAt }, "IX_StockMovement_OfferId_CreatedAt");

            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_StockMovement_CreatedAt");
            entity.Property(e => e.Note).HasMaxLength(300);
            entity.Property(e => e.Reason).HasMaxLength(20);

            entity.HasOne(d => d.Offer).WithMany(p => p.StockMovements)
                .HasForeignKey(d => d.OfferId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.SellerOrder).WithMany(p => p.StockMovements).HasForeignKey(d => d.SellerOrderId);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
