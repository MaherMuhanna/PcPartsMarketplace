using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PCM.DomainEntities.Identity;
using PCM.Infrastructure.DTO.Orders;

namespace PCM.Infrastructure.Orders;

/// <summary>
/// Customer names (and emails) for seller and admin order screens. Users live in AspNetUsers
/// (AuthDbContext), not in PcPartsContext, so they are read in one extra query per page.
/// </summary>
public sealed class CustomerDirectory(UserManager<ApplicationUser> userManager)
{
    /// <summary>Customers by id. Unknown ids are simply missing from the result.</summary>
    public async Task<IReadOnlyDictionary<int, OrderCustomerDto>> GetAsync(IEnumerable<int> userIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userIds);

        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<int, OrderCustomerDto>();
        }

        return await userManager.Users
            .AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new OrderCustomerDto(u.Id, u.FullName, u.Email))
            .ToDictionaryAsync(c => c.Id, cancellationToken);
    }

    /// <summary>One customer; a placeholder if the account no longer exists.</summary>
    public async Task<OrderCustomerDto> GetOneAsync(int userId, CancellationToken cancellationToken = default)
    {
        var customers = await GetAsync([userId], cancellationToken);
        return customers.TryGetValue(userId, out var customer) ? customer : Unknown(userId);
    }

    public static OrderCustomerDto Unknown(int userId) => new(userId, "Unknown customer", null);
}
