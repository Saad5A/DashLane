using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace DashLane.Models;

public class DriverOnboardingViewModel
{
    [Required, StringLength(60)]
    public string Make { get; set; } = "";

    [Required, StringLength(60)]
    public string Model { get; set; } = "";

    [Required, Range(1990, 2035)]
    public int? Year { get; set; }

    [Required, StringLength(16, MinimumLength = 2)]
    [Display(Name = "License plate")]
    public string LicensePlate { get; set; } = "";

    [Required, EnumDataType(typeof(VehicleTier))]
    public VehicleTier Tier { get; set; } = VehicleTier.Standard;

    [Range(1, 6)]
    [Display(Name = "Passenger capacity")]
    public int PassengerCapacity { get; set; } = 4;

    [Display(Name = "Wheelchair accessible")]
    public bool IsWheelchairAccessible { get; set; }

    [StringLength(64)]
    [Display(Name = "License number")]
    public string? LicenseNumber { get; set; }

    [Display(Name = "License expiry")]
    public DateOnly? LicenseExpiry { get; set; }

    [ValidateNever]
    public IFormFile? LicenseFront { get; set; }

    [ValidateNever]
    public IFormFile? LicenseBack { get; set; }

    // Readiness / display (not posted as authoritative state)
    public bool PhoneVerified { get; set; }
    public bool ProfilePhotoUploaded { get; set; }
    public bool RequiresLicenseVerification { get; set; }
    public bool LicenseVerified { get; set; }
    public bool BackgroundCheckVerified { get; set; }
    public bool HasExistingVehicle { get; set; }
    public string? ExistingLicenseDocumentUrl { get; set; }
    public string? Error { get; set; }
}
