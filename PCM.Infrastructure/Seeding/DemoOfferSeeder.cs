using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using PCM.DomainEntities.Identity;
using PCM.Infrastructure.DTO.Offers;
using PCM.Infrastructure.Services;
using PCM.Repositories;

namespace PCM.Infrastructure.Seeding;

/// <summary>
/// Demo offers (DEVELOPMENT ONLY, runs after <see cref="DemoUserSeeder"/> and <see cref="DemoCatalogSeeder"/>):
/// Tech Hub (seller1) offers 24 products and Circuit Store (seller3) 12 of the same products at
/// different prices, so product pages show several offers sorted by price. Stock levels are varied
/// on purpose: some offers are out of stock and some are low, to fill every tab of "My offers".
/// Everything goes through <see cref="ISellerOfferService"/>, so the stock history is real.
/// Idempotent: a seller who already has offers is skipped.
/// </summary>
public sealed class DemoOfferSeeder(
    UserManager<ApplicationUser> userManager,
    IUnitOfWork unitOfWork,
    ISellerOfferService offerService,
    ILogger<DemoOfferSeeder> logger)
{
    /// <summary>One demo offer. Threshold null = the default (5).</summary>
    private sealed record DemoOffer(string ProductSlug, string Condition, decimal Price, int Stock, int? Threshold = null);

    /// <summary>A stock change made after creation, so the history page has more than one row.</summary>
    private sealed record DemoMovement(string ProductSlug, string Type, int Change, string Note);

    private static readonly DemoOffer[] TechHub =
    [
        // CPU
        new("amd-ryzen-7-7800x3d", "New", 329.00m, 12),
        new("amd-ryzen-5-7600x", "New", 159.00m, 3),
        new("intel-core-i5-14600kf", "New", 199.00m, 20),
        new("intel-core-i7-14700k", "Used", 245.00m, 1, 2),
        // GPU
        new("asus-dual-geforce-rtx-4060-oc-8gb", "New", 219.00m, 8),
        new("msi-geforce-rtx-4070-super-12g-ventus-2x-oc", "New", 459.00m, 0),
        new("sapphire-pulse-radeon-rx-7600-8gb", "New", 189.00m, 15),
        new("sapphire-nitro-radeon-rx-7800-xt-16gb", "Used", 365.00m, 2, 1),
        // RAM
        new("corsair-vengeance-ddr5-32gb-2x16gb-6000mt-s-cl30", "New", 79.00m, 30, 10),
        new("g-skill-trident-z5-rgb-32gb-2x16gb-6400mt-s-cl32", "New", 95.00m, 4),
        new("kingston-fury-beast-16gb-2x8gb-ddr4-3200mt-s-cl16", "New", 32.00m, 40, 10),
        new("crucial-pro-32gb-2x16gb-ddr5-5600mt-s-cl46", "New", 69.00m, 0),
        // Storage
        new("samsung-990-pro-2tb-nvme-ssd", "New", 129.00m, 10),
        new("samsung-870-evo-1tb-sata-ssd", "New", 65.00m, 6),
        new("wd-black-sn850x-1tb-nvme-ssd", "New", 72.00m, 5),
        new("seagate-barracuda-4tb-hdd", "New", 75.00m, 9),
        // Motherboard
        new("msi-mag-b650-tomahawk-wifi", "New", 155.00m, 7),
        new("gigabyte-b650m-aorus-elite-ax", "New", 129.00m, 2),
        new("asus-tuf-gaming-b760-plus-wifi-d4", "New", 119.00m, 11),
        new("asrock-b760m-pro-rs", "New", 79.00m, 0),
        // Power supply
        new("corsair-rm850e-850w-80-gold", "New", 89.00m, 14),
        new("seasonic-focus-gx-750-750w-80-gold", "New", 85.00m, 3),
        new("corsair-cx650m-650w-80-bronze", "New", 49.00m, 25, 8),
        new("cooler-master-mwe-gold-650-v2-650w", "Used", 39.00m, 1, 1)
    ];

    private static readonly DemoMovement[] TechHubMovements =
    [
        new("amd-ryzen-7-7800x3d", "Adjustment", -1, "Damaged box, returned to supplier"),
        new("amd-ryzen-7-7800x3d", "Restock", 4, "Supplier delivery"),
        new("samsung-870-evo-1tb-sata-ssd", "Adjustment", 1, "Stock count found one extra unit")
    ];

    private static readonly DemoOffer[] CircuitStore =
    [
        new("amd-ryzen-7-7800x3d", "New", 319.00m, 6),
        new("amd-ryzen-5-7600x", "New", 165.00m, 10),
        new("intel-core-i5-14600kf", "New", 205.00m, 0),
        new("asus-dual-geforce-rtx-4060-oc-8gb", "New", 214.00m, 4),
        new("msi-geforce-rtx-4070-super-12g-ventus-2x-oc", "New", 465.00m, 3),
        new("corsair-vengeance-ddr5-32gb-2x16gb-6000mt-s-cl30", "New", 82.00m, 12),
        new("kingston-fury-beast-16gb-2x8gb-ddr4-3200mt-s-cl16", "Used", 24.00m, 5),
        new("samsung-990-pro-2tb-nvme-ssd", "New", 125.00m, 8),
        new("wd-black-sn850x-1tb-nvme-ssd", "New", 69.00m, 20),
        new("msi-mag-b650-tomahawk-wifi", "New", 149.00m, 1),
        new("corsair-rm850e-850w-80-gold", "New", 92.00m, 9),
        new("seasonic-focus-gx-750-750w-80-gold", "New", 82.00m, 0)
    ];

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var slugs = TechHub.Concat(CircuitStore).Select(o => o.ProductSlug).Distinct().ToList();
        var productIds = await unitOfWork.Products.GetPublishedIdsBySlugsAsync(slugs, cancellationToken);
        if (productIds.Count == 0)
        {
            logger.LogWarning("Demo offers skipped: run Database/seed.sql first (no published seed products found)");
            return;
        }

        await SeedSellerAsync("seller1@pcm.local", TechHub, TechHubMovements, productIds, cancellationToken);
        await SeedSellerAsync("seller3@pcm.local", CircuitStore, [], productIds, cancellationToken);
    }

    private async Task SeedSellerAsync(
        string email,
        IReadOnlyList<DemoOffer> offers,
        IReadOnlyList<DemoMovement> movements,
        IReadOnlyDictionary<string, int> productIds,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(email);
        var profile = user is null ? null : await unitOfWork.SellerProfiles.GetByUserIdAsync(user.Id, cancellationToken);
        if (user is null || profile is null)
        {
            logger.LogInformation("Demo offers skipped: {Email} does not exist", email);
            return;
        }

        if (await unitOfWork.Offers.ExistsAsync(o => o.SellerId == profile.Id, cancellationToken))
        {
            return;
        }

        var offerIds = new Dictionary<string, int>();
        foreach (var demo in offers)
        {
            if (!productIds.TryGetValue(demo.ProductSlug, out var productId))
            {
                logger.LogWarning("Demo offer skipped: product {Slug} is not published", demo.ProductSlug);
                continue;
            }

            var offer = await offerService.CreateAsync(user.Id, new CreateOfferRequest
            {
                ProductId = productId,
                Condition = demo.Condition,
                Price = demo.Price,
                Stock = demo.Stock,
                LowStockThreshold = demo.Threshold
            }, cancellationToken);
            offerIds[demo.ProductSlug] = offer.Id;
        }

        foreach (var movement in movements.Where(m => offerIds.ContainsKey(m.ProductSlug)))
        {
            await offerService.ChangeStockAsync(user.Id, offerIds[movement.ProductSlug], new StockChangeRequest
            {
                Type = movement.Type,
                Change = movement.Change,
                Note = movement.Note
            }, cancellationToken);
        }

        logger.LogInformation("Demo offers created for {Email}: {Count}", email, offerIds.Count);
    }
}
