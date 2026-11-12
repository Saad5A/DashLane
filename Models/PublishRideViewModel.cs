using System.ComponentModel.DataAnnotations;

namespace DashLane.Models;

public class PublishRideViewModel
{
    [Range(typeof(bool), "true", "true", ErrorMessage = "Accept DashLane's Driver Punctuality & Cancellation Policy to publish.")]
    public bool AcceptPunctualityPolicy { get; set; }

    public string? Error { get; set; }

    public string OriginAddress { get; set; } = "";
    public string DestinationAddress { get; set; } = "";
    public DateTime DepartureUtc { get; set; }
    public decimal DistanceKm { get; set; }
    public decimal DurationMin { get; set; }
    public IReadOnlyList<PostedRideStopDraft> Stops { get; set; } = [];
    public string? PickupInstructions { get; set; }
    public string? LuggageAllowance { get; set; }
    public int AvailableSeats { get; set; }
    public bool AutoApproveRiders { get; set; }
    public decimal EstimatedPricePerSeat { get; set; }
    public decimal EstimatedTotalEarnings { get; set; }
    public VehicleTier Tier { get; set; }

    public string DriverName { get; set; } = "";
    public bool LicenseVerified { get; set; }
    public string VehicleLabel { get; set; } = "";
    public string LicensePlate { get; set; } = "";
    public string OriginPlaceId { get; set; } = "";
    public string DestinationPlaceId { get; set; } = "";
}

public class ActiveTripViewModel
{
    public Guid PostedRideId { get; set; }
    public bool IsOnline { get; set; }
    public PostedRideStatus RideStatus { get; set; }
    public string OriginAddress { get; set; } = "";
    public string DestinationAddress { get; set; } = "";
    public string OriginPlaceId { get; set; } = "";
    public string DestinationPlaceId { get; set; } = "";
    public DateTime DepartureUtc { get; set; }
    public decimal DistanceKm { get; set; }
    public decimal DurationMin { get; set; }
    public int AvailableSeats { get; set; }
    public decimal EstimatedPricePerSeat { get; set; }
    public decimal EstimatedTotalEarnings { get; set; }
    public string VehicleLabel { get; set; } = "";
    public string LicensePlate { get; set; } = "";
    public bool LicenseVerified { get; set; }
    public IReadOnlyList<PostedRideStopDraft> Stops { get; set; } = [];
    public IncomingRiderRequestView? PendingRequest { get; set; }
    public ActiveBookingView? ActiveBooking { get; set; }
    public IReadOnlyList<RidePlace> Places { get; set; } = [];
    public IReadOnlyList<RidePreview> Routes { get; set; } = [];
}

public record IncomingRiderRequestView(
    Guid BookingId,
    string RiderName,
    double RiderRating,
    string PickupLabel,
    int Passengers,
    decimal GuaranteedPayout,
    DateTime AcceptanceDeadlineUtc,
    DateTime ServerUtc);

public record ActiveBookingView(
    Guid BookingId,
    RideStatus Status,
    string RiderName,
    string PickupLabel,
    int Passengers,
    decimal Fare,
    DateTime? PickupEtaUtc);

public class ToggleAvailabilityRequest
{
    public bool Online { get; init; }
}

public record ToggleAvailabilityResult(bool IsOnline, string Availability);

public class SimulateRequestInput
{
    public Guid PostedRideId { get; init; }
}
