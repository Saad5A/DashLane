namespace DashLane.Models;

public record RidePlace(string Id, string Name, string Address, double Latitude, double Longitude);
public record RideMapPoint(double Latitude, double Longitude);
public record RidePreview(string PickupId, string DestinationId, decimal DistanceKm, decimal DurationMin,
    IReadOnlyList<RideMapPoint> Points);
public record RideTierOption(VehicleTier Tier, string Name, string Description, string Capacity);
public record RideRequestViewModel(IReadOnlyList<RidePlace> Places, IReadOnlyList<RidePreview> Routes,
    IReadOnlyList<RideTierOption> Tiers, RideSelection? Initial = null);

public record RideSelection(string PickupId, string DestinationId, VehicleTier Tier, int Passengers,
    bool RequiresWheelchairAccess, decimal Fare, decimal SurgeMultiplier, string? ScheduledLocal);

public record RideDraft(RidePlace Pickup, RidePlace Destination, RidePreview Route, TierFare Quote,
    int Passengers, bool RequiresWheelchairAccess, DateTime? ScheduledUtc, DateTimeOffset QuotedAtUtc)
{
    public Guid Id { get; init; }
    public DriverConfirmInput? Confirmation { get; init; }
}
