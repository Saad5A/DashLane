using System.ComponentModel.DataAnnotations;

namespace DashLane.Models;

public record DriverVerificationStatus(
    bool RequiresLicenseVerification,
    bool HasSignupIdDocument,
    bool SignupIdAccepted,
    bool LicenseVerified,
    bool BackgroundCheckVerified,
    bool CanPublishRides,
    string? Message);

public class EstimatePayoutRequest
{
    [Required, EnumDataType(typeof(VehicleTier))]
    public VehicleTier? Tier { get; init; }

    [Required, Range(typeof(decimal), "0.01", "10000")]
    public decimal DistanceKm { get; init; }

    [Required, Range(typeof(decimal), "0.01", "10080")]
    public decimal DurationMin { get; init; }

    [Range(1, 6)]
    public int AvailableSeats { get; init; } = 1;

    [Range(typeof(decimal), "0.01", "10")]
    public decimal SurgeMultiplier { get; init; } = 1m;
}

public record EstimatePayoutResult(
    VehicleTier Tier,
    decimal BaseFee,
    decimal RatePerKm,
    decimal RatePerMin,
    decimal Minimum,
    decimal SurgeMultiplier,
    decimal EstimatedPricePerSeat,
    int AvailableSeats,
    decimal EstimatedTotalEarnings);

public enum DriverRequestAction { Accept, Decline, Arrived, Start, Complete }

public class DriverRequestRespondInput
{
    [Required, EnumDataType(typeof(DriverRequestAction))]
    public DriverRequestAction? Action { get; init; }
}

public class PublishPostedRideRequest
{
    [Required, StringLength(200)]
    public string OriginAddress { get; init; } = "";

    [Required, StringLength(200)]
    public string DestinationAddress { get; init; } = "";

    [Required]
    public DateTime? DepartureTime { get; init; }

    /// <summary>Ordered pickup stops along the way.</summary>
    public IReadOnlyList<string>? IntermediateStops { get; init; }

    [StringLength(400)]
    public string? PickupInstructions { get; init; }

    [StringLength(120)]
    public string? LuggageAllowance { get; init; }

    [Range(1, 6)]
    public int AvailableSeats { get; init; } = 1;

    public bool AutoApproveRiders { get; init; }

    [Required, EnumDataType(typeof(VehicleTier))]
    public VehicleTier? Tier { get; init; }

    [Required, Range(typeof(decimal), "0.01", "10000")]
    public decimal DistanceKm { get; init; }

    [Required, Range(typeof(decimal), "0.01", "10080")]
    public decimal DurationMin { get; init; }
}

public record PostedRideResult(
    Guid Id,
    PostedRideStatus Status,
    string OriginAddress,
    string DestinationAddress,
    DateTime DepartureTime,
    int AvailableSeats,
    decimal EstimatedPricePerSeat,
    bool AutoApproveRiders,
    bool RequiresLicenseVerification);
