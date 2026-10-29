using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using PCM.DomainEntities.Identity;
using PCM.Infrastructure.DTO.Catalog;
using PCM.Infrastructure.Services;
using PCM.Repositories;

namespace PCM.Infrastructure.Seeding;

/// <summary>
/// Adds two demo product proposals from seller1 (DEVELOPMENT ONLY, runs after <see cref="DemoUserSeeder"/>):
/// one Pending, one Rejected with a note, so the review workflow can be tried at once.
/// They go through the real services, so they follow every business rule.
/// Database/seed.sql cannot create them because seller accounts are created by the API.
/// Idempotent: skipped when seller1 already has proposals or the catalog is not seeded.
/// </summary>
public sealed class DemoCatalogSeeder(
    UserManager<ApplicationUser> userManager,
    IUnitOfWork unitOfWork,
    ICategoryService categoryService,
    ISellerProposalService proposalService,
    IProductAdminService productAdminService,
    ILogger<DemoCatalogSeeder> logger)
{
    public const string SellerEmail = "seller1@pcm.local";

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var seller = await userManager.FindByEmailAsync(SellerEmail);
        var profile = seller is null ? null : await unitOfWork.SellerProfiles.GetByUserIdAsync(seller.Id, cancellationToken);
        if (seller is null || profile is null)
        {
            logger.LogInformation("Demo proposals skipped: {Email} does not exist", SellerEmail);
            return;
        }

        if (await unitOfWork.Products.ExistsAsync(p => p.ProposedBySellerId == profile.Id, cancellationToken))
        {
            return;
        }

        var categories = await unitOfWork.Categories.GetAllAsync(cancellationToken);
        var cpu = categories.FirstOrDefault(c => c.Slug == "cpu");
        var ram = categories.FirstOrDefault(c => c.Slug == "ram");
        if (cpu is null || ram is null)
        {
            logger.LogWarning("Demo proposals skipped: run Database/seed.sql first (categories 'cpu' and 'ram' are missing)");
            return;
        }

        var brands = (await unitOfWork.Brands.GetBySlugsAsync(["intel", "kingston"], cancellationToken)).ToDictionary(b => b.Slug);

        // 1. Pending: waits in the admin review queue.
        await proposalService.CreateAsync(seller.Id, new ProductUpsertRequest
        {
            CategoryId = cpu.Id,
            BrandId = brands["intel"].Id,
            Name = "Intel Core i5-14400F",
            ModelNumber = "BX8071514400F",
            Description = "10-core processor without integrated graphics (demo proposal).",
            Specs = await SpecsAsync(cpu.Id, new()
            {
                ["socket"] = "lga1700", ["cores"] = 10m, ["threads"] = 16m, ["base-clock"] = 2.5m,
                ["boost-clock"] = 4.7m, ["tdp"] = 65m, ["integrated-graphics"] = "no"
            }, cancellationToken),
            Images = [new ProductImageRequest { Url = "/images/products/cpu.svg", AltText = "Intel Core i5-14400F" }]
        }, cancellationToken);

        // 2. Rejected: shows the seller the admin's note.
        var rejected = await proposalService.CreateAsync(seller.Id, new ProductUpsertRequest
        {
            CategoryId = ram.Id,
            BrandId = brands["kingston"].Id,
            Name = "Kingston FURY Beast 8GB DDR4 2666MT/s",
            ModelNumber = "KF426C16BB/8",
            Description = "Single 8 GB DDR4 module (demo proposal).",
            Specs = await SpecsAsync(ram.Id, new()
            {
                ["memory-type"] = "ddr4", ["capacity"] = 8m, ["speed"] = 2666m, ["cas-latency"] = 16m, ["rgb"] = "no"
            }, cancellationToken),
            Images = [new ProductImageRequest { Url = "/images/products/ram.svg", AltText = "Kingston FURY Beast 8GB" }]
        }, cancellationToken);

        await productAdminService.RejectAsync(rejected.Id,
            "Single 8 GB DDR4 modules are not sold here. Please propose the 2x8GB kit (KF426C16BBK2/16) instead.", cancellationToken);

        logger.LogInformation("Demo proposals created for {Email}: 1 pending, 1 rejected", SellerEmail);
    }

    /// <summary>Turns attribute-slug -> value pairs into spec requests (string = option slug, decimal = number).</summary>
    private async Task<List<SpecValueRequest>> SpecsAsync(int categoryId, Dictionary<string, object> values, CancellationToken cancellationToken)
    {
        var attributes = (await categoryService.GetAttributesAsync(categoryId, cancellationToken)).ToDictionary(a => a.Slug);

        return values.Select(pair =>
        {
            var attribute = attributes[pair.Key];
            return pair.Value is decimal number
                ? new SpecValueRequest { AttributeId = attribute.Id, NumberValue = number }
                : new SpecValueRequest { AttributeId = attribute.Id, OptionId = attribute.Options.Single(o => o.Slug == (string)pair.Value).Id };
        }).ToList();
    }
}
