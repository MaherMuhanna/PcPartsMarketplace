using Microsoft.AspNetCore.Identity;

namespace PCM.DomainEntities.Identity;

/// <summary>
/// Identity role (table dbo.AspNetRoles, int key). The three roles
/// (Admin, Seller, Customer) are seeded by Database/schema.sql.
/// </summary>
public class ApplicationRole : IdentityRole<int>
{
    public ApplicationRole()
    {
    }

    public ApplicationRole(string roleName) : base(roleName)
    {
    }
}
