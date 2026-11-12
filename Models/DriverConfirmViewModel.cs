using System.ComponentModel.DataAnnotations;

namespace DashLane.Models;

public record DriverRating(double Average, int Count);
public record DriverApproach(int Id, RideMapPoint Position, int EtaMinutes);
public record DriverOption(int Id, string Name, DriverRating? Rating, string? PhotoUrl,
    string Make, string Model, int? Year, string Plate, int Capacity, bool Accessible, DriverApproach Approach);

public record DriverConfirmViewModel(RideDraft Draft, IReadOnlyList<DriverOption> Drivers,
    DriverConfirmInput Input, bool IsPreview, string? Error = null);

public class DriverConfirmInput
{
    public Guid DraftId { get; init; }
    [Required]
    public int? DriverId { get; init; }
    public string? DriverName { get; init; }
    public string? Vehicle { get; init; }
    [StringLength(200)]
    public string? PickupInstructions { get; init; }
    [StringLength(200)]
    public string? DriverNote { get; init; }
}
