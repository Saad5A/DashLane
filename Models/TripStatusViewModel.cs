using System.ComponentModel.DataAnnotations;

namespace DashLane.Models;

public class TripMessage
{
    public int Id { get; set; }
    public Guid RideBookingId { get; set; }
    public RideBooking RideBooking { get; set; } = null!;
    public bool IsDriver { get; set; }
    public string Text { get; set; } = "";
    public DateTime CreatedUtc { get; set; }
}

public class TripIncident
{
    public int Id { get; set; }
    public Guid RideBookingId { get; set; }
    public RideBooking RideBooking { get; set; } = null!;
    public int ReporterId { get; set; }
    public bool IsSos { get; set; }
    public string Text { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public DateTime CreatedUtc { get; set; }
}

public record TripStatusViewModel(Guid Id, string Status, bool IsDriver, string? Driver, string? Vehicle,
    string? Plate, RideMapPoint Pickup, RideMapPoint Destination, RideMapPoint? DriverPosition,
    string? PickupInstructions, DateTime? AcceptanceDeadlineUtc, DateTime? PickupEtaUtc, DateTime ServerUtc,
    bool DriverLate, decimal? CancellationQuote, decimal? CancellationFee, string? CancellationReason,
    decimal LockedFare, decimal Surge, decimal Discount, decimal? IncludedTax, decimal? Paid, string? PaymentMethod,
    string? PaymentReference, bool Reviewed, IReadOnlyList<TripChatLine> Messages, bool CanAdvance);

public record TripChatLine(string Sender, string Text, DateTime CreatedUtc);
public record TripTextInput([Required, StringLength(500)] string Text);
public record TripReviewInput([Range(1, 5)] int Rating, [StringLength(500)] string? Comment);
public record TripAdvanceInput([EnumDataType(typeof(RideStatus))] RideStatus ExpectedStatus);
public record TripCancelInput([Range(typeof(decimal), "0", "5")] decimal ExpectedFee);
public record TripLocationInput([Required, Range(-90d, 90d)] double? Latitude,
    [Required, Range(-180d, 180d)] double? Longitude);
