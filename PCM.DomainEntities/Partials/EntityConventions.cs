using PCM.DomainEntities.Common;

// Attaches the audit / soft-delete contracts to the scaffolded entities.
// The scaffolded classes already have the matching properties (from schema.sql),
// so these declarations add no members; they let the interceptor and the global
// query filter recognise the entities. Kept outside DBEntities/ so re-scaffolding
// never overwrites them.
namespace PCM.DomainEntities.DBEntities;

// [Audit] tables: CreatedAt + UpdatedAt + IsDeleted
public partial class SellerProfile : IAuditable, ISoftDeletable;
public partial class Address : IAuditable, ISoftDeletable;
public partial class Category : IAuditable, ISoftDeletable;
public partial class SpecAttribute : IAuditable, ISoftDeletable;
public partial class SpecOption : IAuditable, ISoftDeletable;
public partial class Brand : IAuditable, ISoftDeletable;
public partial class Product : IAuditable, ISoftDeletable;
public partial class Offer : IAuditable, ISoftDeletable;

// Timestamped but never soft-deleted
public partial class Cart : IAuditable;
public partial class CartItem : IAuditable;
public partial class Order : IAuditable;
public partial class SellerOrder : IAuditable;
