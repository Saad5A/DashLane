using System.ComponentModel.DataAnnotations;

namespace DashLane.Models;

public record RideDriverApproach(int Id, RideMapPoint Position, int EtaMinutes);
public record RideDriverOption(int Id, string Name, RideRating? Rating, string? PhotoUrl,
    string Make, string Model, int? Year, string Plate, int Capacity, bool Accessible, RideDriverApproach Approach);

public record RideConfirmationViewModel(RideDraft Draft, IReadOnlyList<RideDriverOption> Drivers,
    RideDriverSelectionInput Input);

public class RideDriverSelectionInput
{
    public Guid DraftId { get; init; }
    [Required]
    public int? DriverId { get; init; }
    [StringLength(200)]
    public string? PickupInstructions { get; init; }
    [StringLength(200)]
    public string? DriverNote { get; init; }
}
