using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using PCM.DomainEntities.DBEntities;
using PCM.DomainEntities.Enums;
using PCM.DomainEntities.Identity;
using PCM.Infrastructure.Security;
using PCM.Repositories;

namespace PCM.Infrastructure.Seeding;

/// <summary>
/// Creates the demo accounts (DEVELOPMENT ONLY; the API calls this only when
/// the environment is Development and Seed:DemoUsers is true).
/// Idempotent: an account that already exists is left untouched.
/// Users go through UserManager so passwords are hashed exactly like real sign-ups.
/// </summary>
public sealed class DemoUserSeeder(
    UserManager<ApplicationUser> userManager,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<DemoUserSeeder> logger)
{
    private sealed record DemoUser(string Email, string FullName, string Role, string? StoreName = null, SellerStatus? SellerStatus = null);

    private static readonly DemoUser[] Users =
    [
        new("admin@pcm.local", "Site Admin", Roles.Admin),
        new("seller1@pcm.local", "Omar Haddad", Roles.Seller, "Tech Hub", SellerStatus.Approved),
        new("seller2@pcm.local", "Lina Saleh", Roles.Seller, "Byte Corner", SellerStatus.Pending),
        new("customer@pcm.local", "Rami Nasser", Roles.Customer)
    ];

    public async Task SeedAsync(string password, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        var created = 0;

        foreach (var demo in Users)
        {
            if (await userManager.FindByEmailAsync(demo.Email) is not null)
            {
                continue;
            }

            await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

            var user = new ApplicationUser { UserName = demo.Email, Email = demo.Email, FullName = demo.FullName, EmailConfirmed = true };
            EnsureSucceeded(await userManager.CreateAsync(user, password), demo.Email);
            EnsureSucceeded(await userManager.AddToRoleAsync(user, demo.Role), demo.Email);

            if (demo.SellerStatus is { } status)
            {
                await unitOfWork.SellerProfiles.AddAsync(new SellerProfile
                {
                    UserId = user.Id,
                    StoreName = demo.StoreName!,
                    Description = $"{demo.StoreName}: demo store.",
                    ContactPhone = "+962790000000",
                    Status = status.ToString(),
                    ApprovedAt = status == SellerStatus.Approved ? timeProvider.GetUtcNow().UtcDateTime : null
                }, cancellationToken);
            }

            if (demo.Role == Roles.Customer)
            {
                await unitOfWork.Addresses.AddAsync(new Address
                {
                    UserId = user.Id,
                    City = "Amman",
                    Street = "Mecca Street 120",
                    Details = "Building 4, 2nd floor",
                    Phone = "+962791111111",
                    IsDefault = true
                }, cancellationToken);
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            created++;
        }

        logger.LogInformation("Demo users ready ({Created} created, {Existing} already existed)", created, Users.Length - created);
    }

    private static void EnsureSucceeded(IdentityResult result, string email)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Could not seed demo user {email}: " + string.Join("; ", result.Errors.Select(e => e.Description)));
        }
    }
}
