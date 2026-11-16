using DashLane.Models;

namespace DashLane.Services;

/// <summary>Admin-configured tier rates shared by rider quotes and driver payout estimates.</summary>
public static class FareSchedule
{
    public static readonly FareRate[] Rates =
    [
        new(VehicleTier.Standard, 5m, 1.25m, .35m, 8m, 4),
        new(VehicleTier.Comfort, 6m, 1.50m, .40m, 11m, 4),
        new(VehicleTier.XL, 8m, 1.80m, .50m, 15m, 6),
        new(VehicleTier.WheelchairAccessible, 8m, 1.80m, .50m, 15m, 4)
    ];

    public static FareRate? Find(VehicleTier tier) =>
        Rates.SingleOrDefault(rate => rate.Tier == tier);

    public static decimal EstimateTripFare(VehicleTier tier, decimal distanceKm, decimal durationMin,
        decimal surgeMultiplier = 1m)
    {
        var rate = Find(tier) ?? throw new ArgumentOutOfRangeException(nameof(tier));
        return EstimateTripFare(new RideFareRate { Tier = rate.Tier, BaseFee = rate.BaseFee,
            PerKm = rate.PerKm, PerMin = rate.PerMin, Minimum = rate.Minimum, Capacity = rate.Capacity },
            distanceKm, durationMin, surgeMultiplier);
    }

    public static decimal EstimateTripFare(RideFareRate rate, decimal distanceKm, decimal durationMin,
        decimal surgeMultiplier = 1m)
    {
        if (distanceKm <= 0 || durationMin <= 0) throw new ArgumentOutOfRangeException(nameof(distanceKm));
        var surge = Math.Clamp(surgeMultiplier, 1m, 2m);
        return Money(Math.Max(rate.Minimum,
            (rate.BaseFee + distanceKm * rate.PerKm + durationMin * rate.PerMin) * surge));
    }

    public static decimal Money(decimal amount) =>
        Math.Round(amount, 2, MidpointRounding.AwayFromZero);
}

public record FareRate(VehicleTier Tier, decimal BaseFee, decimal PerKm, decimal PerMin,
    decimal Minimum, int Capacity);
