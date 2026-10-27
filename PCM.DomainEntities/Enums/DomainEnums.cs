namespace PCM.DomainEntities.Enums;

// C# names for the values allowed by the CHECK constraints in schema.sql.
// Scaffolded entities expose these columns as strings (Database-First cannot
// generate enum properties), so services convert with ToString() / Enum.Parse.
// Keep the member names identical to the CHECK values.

/// <summary>SellerProfile.Status</summary>
public enum SellerStatus { Pending, Approved, Rejected, Suspended }

/// <summary>Product.Status (seller proposals start as Pending)</summary>
public enum ProductStatus { Pending, Approved, Rejected }

/// <summary>SpecAttribute.DataType</summary>
public enum SpecDataType { Text, Number, Enum }

/// <summary>Offer.Condition and OrderItem.Condition</summary>
public enum OfferCondition { New, Used }

/// <summary>SellerOrder.Status</summary>
public enum SellerOrderStatus { Pending, Confirmed, Shipped, Delivered, Cancelled }

/// <summary>StockMovement.Reason</summary>
public enum StockMovementReason { Restock, Adjustment, Reserve, Release, Sale }

/// <summary>Order.PaymentMethod (cash on delivery only in V1)</summary>
public enum PaymentMethod { COD }
