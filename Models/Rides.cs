namespace DashLane.Models;

public enum VehicleTier { Standard, Comfort, XL, WheelchairAccessible }
public enum DriverAvailability { Offline, Available, Dispatched, OnTrip }
public enum RideStatus { Requested, Assigned, Arrived, InProgress, Completed, Cancelled }
public enum PostedRideStatus { Scheduled, Active, InProgress, Completed, Cancelled }

public class DriverProfile
{
    public int Id { get; set; }
    public int MemberId { get; set; }
    public Member Member { get; set; } = null!;
    public string? LicenseNumber { get; set; }
    public DateOnly? LicenseExpiry { get; set; }
    public string? LicenseDocumentUrl { get; set; }
    public bool LicenseVerified { get; set; }
    public bool BackgroundCheckVerified { get; set; }
    public DriverAvailability Availability { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public Vehicle Vehicle { get; set; } = null!;
    public ICollection<PostedRide> PostedRides { get; set; } = new List<PostedRide>();

    /// <summary>Spec alias for <see cref="LicenseVerified"/>.</summary>
    public bool IsLicenseVerified => LicenseVerified;
    /// <summary>Spec alias for <see cref="BackgroundCheckVerified"/>.</summary>
    public bool IsBackgroundChecked => BackgroundCheckVerified;
    /// <summary>Online toggle mapped from <see cref="Availability"/>.</summary>
    public bool IsAvailable => Availability == DriverAvailability.Available;
    public double CurrentLatitude => Latitude;
    public double CurrentLongitude => Longitude;
}

public class Vehicle
{
    public int Id { get; set; }
    public int DriverProfileId { get; set; }
    public VehicleTier Tier { get; set; }
    public int PassengerCapacity { get; set; }
    public bool WheelchairAccessible { get; set; }
    public string Make { get; set; } = "";
    public string Model { get; set; } = "";
    public int? Year { get; set; }
    public string Plate { get; set; } = "";

    /// <summary>Spec alias for <see cref="PassengerCapacity"/> (1–6).</summary>
    public int Capacity => PassengerCapacity;
    /// <summary>Spec alias for <see cref="Plate"/>.</summary>
    public string LicensePlate => Plate;
    /// <summary>Spec alias for <see cref="WheelchairAccessible"/>.</summary>
    public bool IsWheelchairAccessible => WheelchairAccessible;
}

/// <summary>Driver-scheduled ride offer ("Post a Ride"), distinct from on-demand <see cref="RideBooking"/>.</summary>
public class PostedRide
{
    public Guid Id { get; set; }
    public int DriverProfileId { get; set; }
    public DriverProfile Driver { get; set; } = null!;
    public int VehicleId { get; set; }
    public Vehicle Vehicle { get; set; } = null!;
    public string OriginAddress { get; set; } = "";
    public string DestinationAddress { get; set; } = "";
    public DateTime DepartureTime { get; set; }
    /// <summary>Ordered JSON array of "Pickups Along the Way" stop labels/addresses.</summary>
    public string IntermediateStopsJson { get; set; } = "[]";
    public string? PickupInstructions { get; set; }
    public string? LuggageAllowance { get; set; }
    public int AvailableSeats { get; set; }
    public bool AutoApproveRiders { get; set; }
    public decimal EstimatedPricePerSeat { get; set; }
    public PostedRideStatus Status { get; set; }
    public DateTime CreatedUtc { get; set; }
}

public class RideBooking
{
    public Guid Id { get; set; }
    public int RiderId { get; set; }
    public Member Rider { get; set; } = null!;
    public int? DriverProfileId { get; set; }
    public DriverProfile? Driver { get; set; }
    public VehicleTier Tier { get; set; }
    public RideStatus Status { get; set; }
    public double PickupLatitude { get; set; }
    public double PickupLongitude { get; set; }
    public double DestinationLatitude { get; set; }
    public double DestinationLongitude { get; set; }
    public int Passengers { get; set; }
    public bool RequiresWheelchairAccess { get; set; }
    public decimal DistanceKm { get; set; }
    public decimal DurationMin { get; set; }
    public decimal FareBeforeDiscount { get; set; }
    public decimal? QuotedFare { get; set; }
    public decimal? IncludedTax { get; set; }
    public string? PickupInstructions { get; set; }
    public string? DriverNote { get; set; }
    public decimal SurgeMultiplier { get; set; }
    public int? PromotionId { get; set; }
    public Promotion? Promotion { get; set; }
    public decimal PromoDiscount { get; set; }
    public decimal Fare { get; set; }
    public DateTime RequestedUtc { get; set; }
    public DateTime? AcceptanceDeadlineUtc { get; set; }
    public DateTime? AssignedUtc { get; set; }
    public DateTime? PickupEtaUtc { get; set; }
    public DateTime? ArrivedUtc { get; set; }
    public DateTime? CancelledUtc { get; set; }
    public decimal? CancellationFee { get; set; }
    public string? CancellationReason { get; set; }
    public ICollection<DispatchAttempt> DispatchAttempts { get; set; } = new List<DispatchAttempt>();
}

public class DispatchAttempt
{
    public Guid RideBookingId { get; set; }
    public int DriverProfileId { get; set; }
    public DriverProfile Driver { get; set; } = null!;
}

public class PaymentTransaction
{
    public int Id { get; set; }
    public Guid RideBookingId { get; set; }
    public RideBooking RideBooking { get; set; } = null!;
    public decimal Amount { get; set; }
    public decimal? Discount { get; set; }
    public string Method { get; set; } = "";
    public string Reference { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime CreatedUtc { get; set; }
}

public class ReviewFeedback
{
    public int Id { get; set; }
    public Guid RideBookingId { get; set; }
    public RideBooking RideBooking { get; set; } = null!;
    public int Rating { get; set; }
    public bool IsDriver { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedUtc { get; set; }
}

public class Promotion
{
    public int Id { get; set; }
    public int? EligibleRiderId { get; set; }
    public string Code { get; set; } = "";
    public bool IsActive { get; set; }
    public DateTime StartsUtc { get; set; }
    public DateTime EndsUtc { get; set; }
    public int RedemptionLimit { get; set; }
    public int RedemptionCount { get; set; }
    public decimal DiscountAmount { get; set; }
}
