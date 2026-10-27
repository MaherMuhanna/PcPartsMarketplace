using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PCM.DomainEntities.Identity;

namespace PCM.DomainEntities.Context;

/// <summary>
/// Maps the ASP.NET Core Identity tables (AspNet*) created by Database/schema.sql.
/// Hand-written (not scaffolded) because IdentityDbContext already knows the
/// table shapes; only the custom ApplicationUser columns are configured here.
/// No migrations: the schema script is the source of truth.
///
/// AuthDbContext and PcPartsContext share one SqlConnection per request scope,
/// so a single transaction can cover both (e.g. user + SellerProfile at registration).
/// </summary>
public class AuthDbContext(DbContextOptions<AuthDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, int>(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(u => u.FullName)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(u => u.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");

            entity.Property(u => u.UpdatedAt)
                .HasPrecision(3);
        });
    }
}
