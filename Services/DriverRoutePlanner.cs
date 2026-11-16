using DashLane.Models;

namespace DashLane.Services;

/// <summary>Catalog corridor measurements shared by live driver previews and wizard persistence.</summary>
public static class DriverRoutePlanner
{
    public static (decimal DistanceKm, decimal DurationMin) Measure(
        string originId, string destinationId, IReadOnlyList<PostRouteStopInput> stops)
    {
        if (stops.Count > 5) throw new ArgumentException("Choose at most five intermediate stops.");
        var ids = new List<string> { originId };
        ids.AddRange(stops.Select(stop => stop.PlaceId ?? ""));
        ids.Add(destinationId);
        if (ids.Any(id => !RideRouteCatalog.View.Places.Any(place => place.Id == id)))
            throw new ArgumentException("Choose each stop from the supported GTA locations.");
        if (ids.Distinct().Count() != ids.Count)
            throw new ArgumentException("Choose different locations for origin, destination, and each stop.");
        decimal distance = 0, duration = 0;
        for (var index = 0; index < ids.Count - 1; index++)
        {
            var segment = RideRouteCatalog.Find(ids[index], ids[index + 1])
                ?? throw new ArgumentException("No supported road corridor connects these stops.");
            distance += segment.DistanceKm;
            duration += segment.DurationMin;
        }
        return (FareSchedule.Money(distance), duration);
    }
}
