using DashLane.Models;

namespace DashLane.Services;

public static class RideRouteCatalog
{
    private static readonly RidePlace[] Places =
    [
        new("oakville", "Oakville GO", "214 Cross Ave, Oakville", 43.4551, -79.6824),
        new("port-credit", "Port Credit GO", "30 Queen St E, Mississauga", 43.5566, -79.5865),
        new("square-one", "Square One", "100 City Centre Dr, Mississauga", 43.5931, -79.6426),
        new("pearson", "Pearson Airport, Terminal 1", "6301 Silver Dart Dr, Mississauga", 43.6812, -79.6115),
        new("union", "Union Station", "65 Front St W, Toronto", 43.6453, -79.3806)
    ];

    // Approximate road corridors for the supported stops, with off-peak estimates.
    private static readonly RidePreview[] Corridors =
    [
        Segment(0, 1, 16.5m, 22, new(43.478, -79.691), new(43.533, -79.606)),
        Segment(0, 2, 21.8m, 27, new(43.481, -79.695), new(43.555, -79.665)),
        Segment(0, 3, 35.4m, 33, new(43.496, -79.715), new(43.571, -79.721), new(43.655, -79.650)),
        Segment(0, 4, 37.1m, 42, new(43.478, -79.691), new(43.596, -79.536), new(43.632, -79.426)),
        Segment(1, 2, 8.3m, 18, new(43.568, -79.600), new(43.586, -79.627)),
        Segment(1, 3, 18.6m, 26, new(43.583, -79.567), new(43.654, -79.587)),
        Segment(1, 4, 23.4m, 31, new(43.573, -79.569), new(43.619, -79.488), new(43.632, -79.426)),
        Segment(2, 3, 15.3m, 22, new(43.616, -79.662), new(43.655, -79.650)),
        Segment(2, 4, 28.9m, 36, new(43.589, -79.599), new(43.609, -79.513), new(43.632, -79.426)),
        Segment(3, 4, 28.1m, 33, new(43.690, -79.577), new(43.645, -79.548), new(43.632, -79.426))
    ];

    public static RideRequestViewModel View { get; } = new(Places,
        Corridors.Concat(Corridors.Select(route => new RidePreview(route.DestinationId, route.PickupId,
            route.DistanceKm, route.DurationMin, route.Points.Reverse().ToArray()))).ToArray(),
        [
            new(VehicleTier.Standard, "Standard", "Everyday rides", "Up to 4 passengers"),
            new(VehicleTier.Comfort, "Comfort", "Extra legroom", "Up to 4 passengers"),
            new(VehicleTier.XL, "XL", "Room for larger groups", "Up to 6 passengers"),
            new(VehicleTier.WheelchairAccessible, "Wheelchair-Accessible", "Ramp-equipped vehicle", "Up to 4 passengers")
        ]);

    public static RidePreview? Find(string? pickupId, string? destinationId) =>
        View.Routes.FirstOrDefault(route => route.PickupId == pickupId && route.DestinationId == destinationId);

    public static RideQuoteRequest QuoteRequest(RidePreview route, int passengers, bool accessible, string? promo = null)
    {
        var pickup = route.Points[0];
        return new RideQuoteRequest
        {
            PickupLatitude = pickup.Latitude,
            PickupLongitude = pickup.Longitude,
            DistanceKm = route.DistanceKm,
            DurationMin = route.DurationMin,
            Passengers = passengers,
            RequiresWheelchairAccess = accessible,
            PromoCode = promo
        };
    }

    private static RidePreview Segment(int pickup, int destination, decimal distance, decimal minutes,
        params RideMapPoint[] waypoints)
    {
        var origin = Places[pickup];
        var end = Places[destination];
        return new RidePreview(origin.Id, end.Id, distance, minutes,
            [new(origin.Latitude, origin.Longitude), .. waypoints, new(end.Latitude, end.Longitude)]);
    }
}
