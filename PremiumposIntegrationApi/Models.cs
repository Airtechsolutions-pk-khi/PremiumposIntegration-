using System.ComponentModel.DataAnnotations;

namespace PremiumposIntegrationApi;

public enum PaymentStatus
{
    Pending,
    Paid,
    Failed
}

public enum ShipmentStatus
{
    Pending,
    Booked,
    InTransit,
    Delivered,
    Cancelled
}

public class Payment
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public string OrderId { get; set; } = string.Empty;

    public string Provider { get; set; } = "moyassar";

    public string? ProviderPaymentId { get; set; }

    public decimal Amount { get; set; }

    public string? TenantId { get; set; }

    public string Currency { get; set; } = "SAR";

    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    public string? Metadata { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
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
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public string OrderId { get; set; } = string.Empty;

    public string Provider { get; set; } = "redbox";

    public string? ProviderShipmentId { get; set; }

    public ShipmentStatus Status { get; set; } = ShipmentStatus.Pending;

    public string? TrackingUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class CardDetails
{
    public string Name { get; set; } = string.Empty;
    public string Number { get; set; } = string.Empty;
    public string Cvc { get; set; } = string.Empty;
    public string Month { get; set; } = string.Empty;
    public string Year { get; set; } = string.Empty;
}
