namespace DashLane.Models;

public enum RideResponseAction { Accept, Decline, Arrived, Start, Complete }

public record RideRating(double Average, int Count);

public class RideConfirmation
{
    public Guid DraftId { get; init; }
    public int? DriverId { get; init; }
    public string? DriverName { get; init; }
    public string? Vehicle { get; init; }
    public string? PickupInstructions { get; init; }
    public string? DriverNote { get; init; }
}

public class RideFareRate
{
    public VehicleTier Tier { get; set; }
    public decimal BaseFee { get; set; }
    public decimal PerKm { get; set; }
    public decimal PerMin { get; set; }
    public decimal Minimum { get; set; }
    public int Capacity { get; set; }
}

/// <summary>Immutable booking-time pricing, independent of later rate or demand changes.</summary>
public class RideFareSnapshot
{
    public Guid RideBookingId { get; set; }
    public RideBooking RideBooking { get; set; } = null!;
    public decimal BaseFee { get; set; }
    public decimal RatePerKm { get; set; }
    public decimal RatePerMin { get; set; }
    public decimal Minimum { get; set; }
    public decimal DistanceCharge { get; set; }
    public decimal DurationCharge { get; set; }
    public decimal SurgeMultiplier { get; set; }
    public decimal SurgeCharge { get; set; }
    public decimal MinimumAdjustment { get; set; }
    public decimal LockedTotal { get; set; }
    public decimal IncludedTax { get; set; }
    public decimal Discount { get; set; }
    public decimal Total { get; set; }
}
