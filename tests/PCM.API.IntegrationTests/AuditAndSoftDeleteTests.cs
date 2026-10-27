using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PCM.API.IntegrationTests.Infrastructure;
using PCM.DomainEntities.Context;
using PCM.DomainEntities.DBEntities;
using PCM.Repositories;

namespace PCM.API.IntegrationTests;

/// <summary>
/// Verifies the conventions applied by AuditSaveChangesInterceptor and the global
/// soft-delete query filter, through the real Unit of Work against SQL Server.
/// </summary>
[Collection(nameof(ApiCollection))]
public sealed class AuditAndSoftDeleteTests(ApiFactory factory)
{
    [Fact]
    public async Task Insert_update_and_remove_follow_audit_and_soft_delete_conventions()
    {
        var ct = TestContext.Current.CancellationToken;
        var name = $"Test Brand {Guid.NewGuid():N}";
        int brandId;

        // Insert: CreatedAt is set, UpdatedAt stays null.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var brand = new Brand { Name = name };
            await unitOfWork.Brands.AddAsync(brand, ct);
            await unitOfWork.SaveChangesAsync(ct);

            brandId = brand.Id;
            Assert.True(brand.CreatedAt > DateTime.UtcNow.AddMinutes(-5));
            Assert.Null(brand.UpdatedAt);
        }

        // Update: UpdatedAt is set.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var brand = await unitOfWork.Brands.GetByIdAsync(brandId, ct);
            Assert.NotNull(brand);

            brand.LogoUrl = "https://example.com/logo.png";
            await unitOfWork.SaveChangesAsync(ct);
            Assert.NotNull(brand.UpdatedAt);
        }

        // Remove: row is kept with IsDeleted = 1 and disappears from normal queries.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var brand = await unitOfWork.Brands.GetByIdAsync(brandId, ct);
            Assert.NotNull(brand);

            unitOfWork.Brands.Remove(brand);
            await unitOfWork.SaveChangesAsync(ct);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<PcPartsContext>();

            Assert.False(await context.Brands.AnyAsync(b => b.Id == brandId, ct));

            var deleted = await context.Brands
                .IgnoreQueryFilters([PcPartsContext.SoftDeleteFilter])
                .SingleAsync(b => b.Id == brandId, ct);
            Assert.True(deleted.IsDeleted);
        }
    }
}
