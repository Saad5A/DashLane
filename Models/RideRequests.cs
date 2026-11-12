using System.ComponentModel.DataAnnotations;

namespace DashLane.Models;

public class RideRouteEstimate
{
    [Required, Range(-90d, 90d)]
    public double? PickupLatitude { get; init; }

    [Required, Range(-180d, 180d)]
    public double? PickupLongitude { get; init; }

    [Range(typeof(decimal), "0.01", "10000")]
    public decimal DistanceKm { get; init; }

    [Range(typeof(decimal), "0.01", "10080")]
    public decimal DurationMin { get; init; }

    [Range(1, 6)]
    public int Passengers { get; init; } = 1;

    public bool RequiresWheelchairAccess { get; init; }
}

public class RideQuoteRequest : RideRouteEstimate
{
    public string? PickupId { get; init; }
    public string? DestinationId { get; init; }

    [StringLength(32)]
    public string? PromoCode { get; init; }
}

public class RideDispatchRequest : RideRouteEstimate
{
    public Guid RequestId { get; init; }

    [Required, EnumDataType(typeof(VehicleTier))]
    public VehicleTier? Tier { get; init; }

    [Required, Range(-90d, 90d)]
    public double? DestinationLatitude { get; init; }

    [Required, Range(-180d, 180d)]
    public double? DestinationLongitude { get; init; }
}

public record TierFare(VehicleTier Tier, decimal BaseFee, decimal RatePerKm, decimal RatePerMin,
    decimal Minimum, decimal SurgeMultiplier, decimal FareBeforeDiscount, decimal PromoDiscount, decimal Fare);

public record DispatchResult(Guid Id, RideStatus Status, DateTime? AcceptanceDeadlineUtc,
    decimal Fare, decimal SurgeMultiplier, string? CancellationReason);
