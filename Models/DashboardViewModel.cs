namespace DashLane.Models;

public class DashboardViewModel
{
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string? ProfilePhotoUrl { get; set; }
    public bool IsPhoneVerified { get; set; }
    public bool IsAgeVerified { get; set; }
    public bool IsIdVerified { get; set; }
    public bool IsDriverVerified { get; set; }
    public double AverageRating { get; set; }
    public int RatingCount { get; set; }
    public int TotalTripsCount { get; set; }
    public bool HasRegisteredVehicle => VehicleSummary != null;
    public DashboardVehicleViewModel? VehicleSummary { get; set; }
    public bool HasActiveTrip => ActiveTripId.HasValue;
    public Guid? ActiveTripId { get; set; }
    public bool IsPostedTrip { get; set; }
    public string Origin { get; set; } = "";
    public string Destination { get; set; } = "";
    public string TripStatus { get; set; } = "";
    public DateTime? DepartureTime { get; set; }
    public decimal? QuotedFare { get; set; }
    public List<RecentTripItemViewModel> RecentTrips { get; set; } = new();
    public string LanguagePreference { get; set; } = "Not specified";
    public bool RequiresWheelchairAccess { get; set; }
    public string? HomeCity { get; set; }
}

public record DashboardVehicleViewModel(string Make, string Model, int? Year,
    string LicensePlate, VehicleTier Tier, bool IsWheelchairAccessible, int PassengerCapacity);

public record RecentTripItemViewModel(Guid Id, DateTime Date, string Origin,
    string Destination, string Role, decimal Fare, string Status, bool IsPostedTrip);
