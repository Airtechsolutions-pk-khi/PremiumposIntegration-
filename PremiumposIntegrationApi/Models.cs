using System.ComponentModel.DataAnnotations;

namespace PremiumposIntegrationApi;

public enum PaymentStatus
{
    Initiated = 1,
    Pending = 2,
    Paid = 3,
    Failed = 4,
    Expired = 5,
    Cancelled = 6,
    Refunding = 7,
    Refunded = 8
}
// Mirrors FoodCarrier's existing PaymentMode ints — keep in sync
public enum PaymentMethodType
{
    CashOnDelivery = 1,
    Card = 2,
    BenefitPay = 3
}

public enum ShipmentStatus
{
    Booked = 1,
    Assigned = 2,
    PickedUp = 3,
    Delivered = 4,
    Failed = 5,
    Cancelled = 6
}


public class Payment
{
    public int Id { get; set; }
    public string OrderId { get; set; } = "";
    public string ProviderInvoiceId { get; set; } = "";
    public decimal Amount { get; set; }
    public PaymentStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}



public class PaymentLog
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid PaymentId { get; set; }

    [Required]
    public string Operation { get; set; } = string.Empty;

    [Required]
    public string Provider { get; set; } = "moyassar";

    [Required]
    public string Status { get; set; } = "pending";

    public string? Message { get; set; }

    public string? SerializedLog { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}


public class Shipment
{
    public int Id { get; set; }
    public string OrderId { get; set; } = "";
    public string ProviderShipmentId { get; set; } = "";
    public string? TrackingUrl { get; set; }
    public ShipmentStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}


public class CardDetails
{
    public string Name { get; set; } = string.Empty;
    public string Number { get; set; } = string.Empty;
    public string Cvc { get; set; } = string.Empty;
    public string Month { get; set; } = string.Empty;
    public string Year { get; set; } = string.Empty;
}
