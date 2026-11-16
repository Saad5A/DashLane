using System.ComponentModel.DataAnnotations;

namespace DashLane.Models;

public class PostRouteStopInput
{
    [Required, StringLength(200)]
    public string Address { get; set; } = "";

    [StringLength(40)]
    public string? PlaceId { get; set; }

    [Range(1, 600)]
    [Display(Name = "ETA offset (min)")]
    public int ArrivalOffsetMin { get; set; } = 10;
}

public class PostRouteViewModel
{
    [Required, StringLength(40)]
    public string OriginPlaceId { get; set; } = "";

    [Required, StringLength(40)]
    public string DestinationPlaceId { get; set; } = "";

    [Required(ErrorMessage = "Choose a departure date and time.")]
    [DataType(DataType.DateTime)]
    [Display(Name = "Departure - Eastern time")]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-ddTHH:mm}", ApplyFormatInEditMode = true)]
    public DateTime? DepartureTime { get; set; }

    [MaxLength(5)]
    public List<PostRouteStopInput> Stops { get; set; } = [];

    /// <summary>JSON mirror of ordered stops for clients that serialize the builder.</summary>
    public string? StopsJson { get; set; }

    [Range(1, 6)]
    public int AvailableSeats { get; set; } = 3;

    public decimal DistanceKm { get; set; }
    public decimal DurationMin { get; set; }
    public string? Error { get; set; }

    public IReadOnlyList<RidePlace> Places { get; set; } = [];
    public IReadOnlyList<RidePreview> Routes { get; set; } = [];
}

public record PostedRideStopDraft(string Address, string? PlaceId, int ArrivalOffsetMin, int Order);

public record PostedRideDraft(
    string OriginPlaceId,
    string DestinationPlaceId,
    string OriginAddress,
    string DestinationAddress,
    DateTime DepartureUtc,
    decimal DistanceKm,
    decimal DurationMin,
    IReadOnlyList<PostedRideStopDraft> Stops,
    string? PickupInstructions = null,
    string? LuggageAllowance = null,
    int AvailableSeats = 1,
    bool AutoApproveRiders = true,
    decimal EstimatedPricePerSeat = 0m,
    decimal EstimatedTotalEarnings = 0m,
    VehicleTier? Tier = null);
