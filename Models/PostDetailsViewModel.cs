using System.ComponentModel.DataAnnotations;

namespace DashLane.Models;

public class PostDetailsViewModel
{
    [StringLength(400)]
    [Display(Name = "Pickup instructions")]
    public string? PickupInstructions { get; set; }

    [Required, StringLength(40)]
    [Display(Name = "Luggage allowance")]
    public string LuggageAllowance { get; set; } = "Carry-On";

    [Range(1, 6)]
    [Display(Name = "Available seats")]
    public int AvailableSeats { get; set; } = 1;

    [Display(Name = "Auto-approve riders")]
    public bool AutoApproveRiders { get; set; } = true;

    public decimal EstimatedPricePerSeat { get; set; }
    public decimal EstimatedTotalEarnings { get; set; }
    public string? Error { get; set; }

    // Read-only context for the form / AJAX payout call
    public int MaxSeats { get; set; } = 4;
    public VehicleTier Tier { get; set; } = VehicleTier.Standard;
    public decimal DistanceKm { get; set; }
    public decimal DurationMin { get; set; }
    public string OriginAddress { get; set; } = "";
    public string DestinationAddress { get; set; } = "";
    public DateTime DepartureUtc { get; set; }
    public int StopCount { get; set; }
}

public static class LuggageOptions
{
    public static readonly string[] All = ["Small Bag", "Carry-On", "Large Suitcase"];
}

public static class BoardingChips
{
    public static readonly string[] All =
    [
        "Curbside pickup only",
        "Max 5 min wait",
        "Trunk space available",
        "Wheelchair ramp ready"
    ];
}
