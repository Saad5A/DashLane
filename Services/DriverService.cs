using System.Text.Json;
using DashLane.Data;
using DashLane.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace DashLane.Services;

public class DriverService(DashLaneContext db, TimeProvider clock, IWebHostEnvironment environment)
{
    private static readonly string[] LicenseExtensions = [".jpg", ".jpeg", ".png", ".webp", ".pdf"];

    public async Task<DriverProfile?> FindByMemberAsync(int memberId, CancellationToken ct) =>
        await db.DriverProfiles.Include(driver => driver.Vehicle).Include(driver => driver.Member)
            .SingleOrDefaultAsync(driver => driver.MemberId == memberId, ct);

    public async Task<DriverVerificationStatus> VerificationStatusAsync(int memberId, CancellationToken ct)
    {
        var member = await db.Members.AsNoTracking().SingleOrDefaultAsync(item => item.Id == memberId, ct);
        if (member == null)
            return new(true, false, false, false, false, false, "Sign in to post a ride.");

        var driver = await db.DriverProfiles.AsNoTracking()
            .SingleOrDefaultAsync(item => item.MemberId == memberId, ct);

        var hasDocument = !string.IsNullOrWhiteSpace(member.DocumentFileName);
        var signupAccepted = string.Equals(member.DocumentCheckDecision, "accept", StringComparison.OrdinalIgnoreCase);
        var licenseVerified = driver?.LicenseVerified == true || signupAccepted;
        var backgroundChecked = driver?.BackgroundCheckVerified == true;
        // Drivers who already passed signup ID verification skip the extra license gate.
        var requiresLicense = !licenseVerified;
        var canPublish = !requiresLicense && backgroundChecked && driver?.Vehicle != null;

        var message = requiresLicense
            ? "Upload and verify your government ID or driver license before publishing a ride."
            : !backgroundChecked
                ? "A completed background check is required before publishing a ride."
                : driver?.Vehicle == null
                    ? "Add a vehicle before publishing a ride."
                    : null;

        return new(requiresLicense, hasDocument, signupAccepted, licenseVerified, backgroundChecked, canPublish, message);
    }

    public async Task<DriverOnboardingViewModel?> BuildOnboardingAsync(int memberId, CancellationToken ct)
    {
        var member = await db.Members.AsNoTracking().SingleOrDefaultAsync(item => item.Id == memberId, ct);
        if (member == null)
            return null;

        var driver = await FindByMemberAsync(memberId, ct);
        var verification = await VerificationStatusAsync(memberId, ct);
        var model = new DriverOnboardingViewModel
        {
            PhoneVerified = member.PhoneVerified,
            ProfilePhotoUploaded = !string.IsNullOrWhiteSpace(member.PhotoFileName),
            RequiresLicenseVerification = verification.RequiresLicenseVerification,
            LicenseVerified = verification.LicenseVerified,
            BackgroundCheckVerified = verification.BackgroundCheckVerified,
            HasExistingVehicle = driver?.Vehicle != null,
            ExistingLicenseDocumentUrl = driver?.LicenseDocumentUrl
        };

        if (driver?.Vehicle is { } vehicle)
        {
            model.Make = vehicle.Make;
            model.Model = vehicle.Model;
            model.Year = vehicle.Year;
            model.LicensePlate = vehicle.Plate;
            model.Tier = vehicle.Tier;
            model.PassengerCapacity = vehicle.PassengerCapacity;
            model.IsWheelchairAccessible = vehicle.WheelchairAccessible;
        }

        if (driver != null)
        {
            model.LicenseNumber = driver.LicenseNumber;
            model.LicenseExpiry = driver.LicenseExpiry;
        }

        return model;
    }

    public async Task<(bool Ok, string? Error)> SaveOnboardingAsync(
        int memberId, DriverOnboardingViewModel input, CancellationToken ct)
    {
        var member = await db.Members.SingleOrDefaultAsync(item => item.Id == memberId, ct);
        if (member == null)
            return (false, "Sign in to continue.");

        if (input.PassengerCapacity is < 1 or > 6)
            return (false, "Passenger capacity must be between 1 and 6 seats.");

        if (input.IsWheelchairAccessible)
            input.Tier = VehicleTier.WheelchairAccessible;
        else if (input.Tier == VehicleTier.WheelchairAccessible)
            input.IsWheelchairAccessible = true;

        var maxCapacity = FareSchedule.Find(input.Tier)?.Capacity ?? 6;
        if (input.PassengerCapacity > maxCapacity)
            return (false, $"This vehicle tier supports up to {maxCapacity} seats.");

        var verification = await VerificationStatusAsync(memberId, ct);
        var requiresLicense = verification.RequiresLicenseVerification;
        string? licenseUrl = null;

        if (requiresLicense)
        {
            if (string.IsNullOrWhiteSpace(input.LicenseNumber))
                return (false, "Enter your driver's license number.");
            if (input.LicenseExpiry is not DateOnly expiry || expiry <= DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime))
                return (false, "Enter a future license expiry date.");
            if (input.LicenseFront is not { Length: > 0 } || input.LicenseBack is not { Length: > 0 })
                return (false, "Upload the front and back of your driver's license.");

            try
            {
                var front = await SaveLicenseFileAsync(memberId, input.LicenseFront, "front");
                var back = await SaveLicenseFileAsync(memberId, input.LicenseBack, "back");
                licenseUrl = JsonSerializer.Serialize(new { front, back });
            }
            catch (InvalidOperationException ex)
            {
                return (false, ex.Message);
            }
        }

        var driver = await db.DriverProfiles.Include(item => item.Vehicle)
            .SingleOrDefaultAsync(item => item.MemberId == memberId, ct);
        if (driver == null)
        {
            driver = new DriverProfile
            {
                MemberId = memberId,
                Availability = DriverAvailability.Offline,
                BackgroundCheckVerified = true
            };
            db.DriverProfiles.Add(driver);
        }

        driver.LicenseNumber = string.IsNullOrWhiteSpace(input.LicenseNumber)
            ? driver.LicenseNumber : input.LicenseNumber.Trim();
        driver.LicenseExpiry = input.LicenseExpiry ?? driver.LicenseExpiry;
        if (licenseUrl != null)
        {
            driver.LicenseDocumentUrl = licenseUrl;
            driver.LicenseVerified = true;
        }
        else if (!requiresLicense)
        {
            driver.LicenseVerified = true;
        }

        driver.Vehicle ??= new Vehicle();
        driver.Vehicle.Make = input.Make.Trim();
        driver.Vehicle.Model = input.Model.Trim();
        driver.Vehicle.Year = input.Year;
        driver.Vehicle.Plate = input.LicensePlate.Trim().ToUpperInvariant();
        driver.Vehicle.Tier = input.Tier;
        driver.Vehicle.PassengerCapacity = input.PassengerCapacity;
        driver.Vehicle.WheelchairAccessible =
            input.IsWheelchairAccessible || input.Tier == VehicleTier.WheelchairAccessible;

        await db.SaveChangesAsync(ct);
        return (true, null);
    }

    private async Task<string> SaveLicenseFileAsync(int memberId, IFormFile file, string side)
    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!LicenseExtensions.Contains(extension))
            throw new InvalidOperationException("Use a JPG, PNG, WebP, or PDF for license uploads.");
        if (file.Length == 0 || file.Length > 10 * 1024 * 1024)
            throw new InvalidOperationException("Each license file must be under 10 MB.");

        var relativeDir = Path.Combine("uploads", "licenses");
        var absoluteDir = Path.Combine(environment.WebRootPath, relativeDir);
        Directory.CreateDirectory(absoluteDir);

        var fileName = $"{memberId}-{side}-{clock.GetUtcNow().UtcDateTime:yyyyMMddHHmmss}{extension}";
        var absolutePath = Path.Combine(absoluteDir, fileName);
        await using (var stream = File.Create(absolutePath))
            await file.CopyToAsync(stream);

        return "/" + Path.Combine(relativeDir, fileName).Replace('\\', '/');
    }

    public EstimatePayoutResult EstimatePayout(EstimatePayoutRequest request)
    {
        var tier = request.Tier!.Value;
        var rate = FareSchedule.Find(tier)
            ?? throw new ArgumentOutOfRangeException(nameof(request.Tier));
        if (request.AvailableSeats > rate.Capacity)
            throw new ArgumentOutOfRangeException(nameof(request.AvailableSeats),
                $"This tier supports up to {rate.Capacity} seats.");

        var surge = request.SurgeMultiplier <= 0 ? 1m : request.SurgeMultiplier;
        var perSeat = FareSchedule.EstimateTripFare(tier, request.DistanceKm, request.DurationMin, surge);
        var total = FareSchedule.Money(perSeat * request.AvailableSeats);
        return new(tier, rate.BaseFee, rate.PerKm, rate.PerMin, rate.Minimum, surge,
            perSeat, request.AvailableSeats, total);
    }

    public async Task<(PostedRideResult? Result, string? Error)> PublishRideAsync(
        int memberId, PublishPostedRideRequest request, CancellationToken ct)
    {
        var verification = await VerificationStatusAsync(memberId, ct);
        if (verification.RequiresLicenseVerification)
            return (new PostedRideResult(Guid.Empty, PostedRideStatus.Cancelled, request.OriginAddress,
                request.DestinationAddress, request.DepartureTime ?? default, request.AvailableSeats, 0,
                request.AutoApproveRiders, true), verification.Message);

        if (!verification.CanPublishRides)
            return (null, verification.Message);

        var driver = await FindByMemberAsync(memberId, ct);
        if (driver?.Vehicle == null)
            return (null, "Add a vehicle before publishing a ride.");

        if (request.DepartureTime is not DateTime departure || departure <= clock.GetUtcNow().UtcDateTime)
            return (null, "Choose a departure time in the future.");

        if (request.AvailableSeats < 1 || request.AvailableSeats > driver.Vehicle.PassengerCapacity)
            return (null, $"Available seats must be between 1 and {driver.Vehicle.PassengerCapacity}.");

        if (driver.Vehicle.Tier != request.Tier)
            return (null, "The selected tier must match your registered vehicle.");

        var payout = EstimatePayout(new EstimatePayoutRequest
        {
            Tier = request.Tier,
            DistanceKm = request.DistanceKm,
            DurationMin = request.DurationMin,
            AvailableSeats = request.AvailableSeats
        });

        var stops = request.IntermediateStops?
            .Where(stop => !string.IsNullOrWhiteSpace(stop))
            .Select(stop => stop.Trim())
            .ToArray() ?? [];

        var ride = new PostedRide
        {
            Id = Guid.NewGuid(),
            DriverProfileId = driver.Id,
            VehicleId = driver.Vehicle.Id,
            OriginAddress = request.OriginAddress.Trim(),
            DestinationAddress = request.DestinationAddress.Trim(),
            DepartureTime = departure,
            IntermediateStopsJson = JsonSerializer.Serialize(stops),
            PickupInstructions = string.IsNullOrWhiteSpace(request.PickupInstructions)
                ? null : request.PickupInstructions.Trim(),
            LuggageAllowance = string.IsNullOrWhiteSpace(request.LuggageAllowance)
                ? null : request.LuggageAllowance.Trim(),
            AvailableSeats = request.AvailableSeats,
            AutoApproveRiders = request.AutoApproveRiders,
            EstimatedPricePerSeat = payout.EstimatedPricePerSeat,
            Status = PostedRideStatus.Active,
            CreatedUtc = clock.GetUtcNow().UtcDateTime
        };

        // Go online so live dispatch can offer this driver rides immediately.
        driver.Availability = DriverAvailability.Available;
        var origin = RideRouteCatalog.View.Places.FirstOrDefault(place =>
            request.OriginAddress.Contains(place.Name, StringComparison.OrdinalIgnoreCase));
        if (origin != null)
        {
            driver.Latitude = origin.Latitude;
            driver.Longitude = origin.Longitude;
        }

        db.PostedRides.Add(ride);
        await db.SaveChangesAsync(ct);
        return (new PostedRideResult(ride.Id, ride.Status, ride.OriginAddress, ride.DestinationAddress,
            ride.DepartureTime, ride.AvailableSeats, ride.EstimatedPricePerSeat, ride.AutoApproveRiders, false), null);
    }

    public async Task<(PostedRideResult? Result, string? Error)> PublishDraftAsync(
        int memberId, PostedRideDraft draft, bool acceptPolicy, CancellationToken ct)
    {
        if (!acceptPolicy)
            return (null, "Accept DashLane's Driver Punctuality & Cancellation Policy to publish.");
        if (draft.EstimatedPricePerSeat <= 0 || draft.AvailableSeats < 1)
            return (null, "Complete Instructions & Fares before publishing.");

        var driver = await FindByMemberAsync(memberId, ct);
        if (driver?.Vehicle == null)
            return (null, "Add a vehicle before publishing a ride.");

        return await PublishRideAsync(memberId, new PublishPostedRideRequest
        {
            OriginAddress = draft.OriginAddress,
            DestinationAddress = draft.DestinationAddress,
            DepartureTime = draft.DepartureUtc,
            IntermediateStops = draft.Stops.OrderBy(stop => stop.Order).Select(stop => stop.Address).ToArray(),
            PickupInstructions = draft.PickupInstructions,
            LuggageAllowance = draft.LuggageAllowance,
            AvailableSeats = draft.AvailableSeats,
            AutoApproveRiders = draft.AutoApproveRiders,
            Tier = draft.Tier ?? driver.Vehicle.Tier,
            DistanceKm = draft.DistanceKm,
            DurationMin = draft.DurationMin
        }, ct);
    }

    public async Task<ToggleAvailabilityResult?> ToggleAvailabilityAsync(int memberId, bool online, CancellationToken ct)
    {
        var driver = await db.DriverProfiles.SingleOrDefaultAsync(item => item.MemberId == memberId, ct);
        if (driver == null)
            return null;

        if (online)
        {
            if (driver.Availability is DriverAvailability.Dispatched or DriverAvailability.OnTrip)
                return new(false, driver.Availability.ToString());
            driver.Availability = DriverAvailability.Available;
        }
        else if (driver.Availability == DriverAvailability.Available)
        {
            driver.Availability = DriverAvailability.Offline;
        }
        else if (driver.Availability == DriverAvailability.Offline)
        {
            return new(false, driver.Availability.ToString());
        }
        else
        {
            return new(driver.Availability != DriverAvailability.Offline, driver.Availability.ToString());
        }

        await db.SaveChangesAsync(ct);
        return new(driver.Availability == DriverAvailability.Available, driver.Availability.ToString());
    }

    public async Task<PostedRide?> FindPostedRideAsync(Guid id, int memberId, CancellationToken ct) =>
        await db.PostedRides.Include(ride => ride.Vehicle).Include(ride => ride.Driver)
            .ThenInclude(driver => driver.Member)
            .SingleOrDefaultAsync(ride => ride.Id == id && ride.Driver.MemberId == memberId, ct);

    public async Task<ActiveTripViewModel?> BuildActiveTripAsync(Guid postedRideId, int memberId, CancellationToken ct)
    {
        var ride = await FindPostedRideAsync(postedRideId, memberId, ct);
        if (ride == null)
            return null;

        var stops = ParseStops(ride.IntermediateStopsJson);
        var pending = await FindPendingRequestAsync(ride.DriverProfileId, ct);
        var active = await FindActiveBookingAsync(ride.DriverProfileId, ct);
        var originPlace = MatchPlace(ride.OriginAddress);
        var destinationPlace = MatchPlace(ride.DestinationAddress);
        var corridor = originPlace != null && destinationPlace != null
            ? RideRouteCatalog.Find(originPlace.Id, destinationPlace.Id) : null;

        return new ActiveTripViewModel
        {
            PostedRideId = ride.Id,
            IsOnline = ride.Driver.Availability == DriverAvailability.Available ||
                       ride.Driver.Availability == DriverAvailability.Dispatched ||
                       ride.Driver.Availability == DriverAvailability.OnTrip,
            RideStatus = ride.Status,
            OriginAddress = ride.OriginAddress,
            DestinationAddress = ride.DestinationAddress,
            OriginPlaceId = originPlace?.Id ?? "",
            DestinationPlaceId = destinationPlace?.Id ?? "",
            DepartureUtc = ride.DepartureTime,
            DistanceKm = corridor?.DistanceKm ?? 0,
            DurationMin = corridor?.DurationMin ?? 0,
            AvailableSeats = ride.AvailableSeats,
            EstimatedPricePerSeat = ride.EstimatedPricePerSeat,
            EstimatedTotalEarnings = FareSchedule.Money(ride.EstimatedPricePerSeat * ride.AvailableSeats),
            VehicleLabel = $"{ride.Vehicle.Year} {ride.Vehicle.Make} {ride.Vehicle.Model}".Trim(),
            LicensePlate = ride.Vehicle.Plate,
            LicenseVerified = ride.Driver.LicenseVerified,
            Stops = stops,
            PendingRequest = pending,
            ActiveBooking = active,
            Places = RideRouteCatalog.View.Places,
            Routes = RideRouteCatalog.View.Routes
        };
    }

    public async Task<(IncomingRiderRequestView? Request, string? Error)> SimulateIncomingRequestAsync(
        int memberId, Guid postedRideId, CancellationToken ct)
    {
        var posted = await FindPostedRideAsync(postedRideId, memberId, ct);
        if (posted == null)
            return (null, "Posted ride not found.");
        if (posted.Status is PostedRideStatus.Cancelled or PostedRideStatus.Completed)
            return (null, "This posted ride is no longer active.");

        var driver = posted.Driver;
        if (driver.Availability is DriverAvailability.Dispatched or DriverAvailability.OnTrip)
            return (null, "Finish or respond to your current dispatch before simulating another request.");

        var origin = MatchPlace(posted.OriginAddress) ?? RideRouteCatalog.View.Places[0];
        var destination = MatchPlace(posted.DestinationAddress)
            ?? RideRouteCatalog.View.Places.First(place => place.Id != origin.Id);
        var corridor = RideRouteCatalog.Find(origin.Id, destination.Id)
            ?? RideRouteCatalog.View.Routes.First(route => route.PickupId == origin.Id);

        driver.Availability = DriverAvailability.Available;
        driver.Latitude = origin.Latitude;
        driver.Longitude = origin.Longitude;
        driver.LicenseVerified = true;
        driver.BackgroundCheckVerified = true;

        var rider = await EnsureDemoRiderAsync(ct);
        var passengers = Math.Clamp(Math.Min(2, posted.AvailableSeats), 1, posted.Vehicle.PassengerCapacity);
        var now = clock.GetUtcNow().UtcDateTime;
        var fare = posted.EstimatedPricePerSeat * passengers;

        var booking = new RideBooking
        {
            Id = Guid.NewGuid(),
            RiderId = rider.Id,
            DriverProfileId = driver.Id,
            Driver = driver,
            Tier = posted.Vehicle.Tier,
            Status = RideStatus.Requested,
            PickupLatitude = origin.Latitude,
            PickupLongitude = origin.Longitude,
            DestinationLatitude = destination.Latitude,
            DestinationLongitude = destination.Longitude,
            Passengers = passengers,
            RequiresWheelchairAccess = posted.Vehicle.WheelchairAccessible,
            DistanceKm = corridor.DistanceKm,
            DurationMin = corridor.DurationMin,
            FareBeforeDiscount = fare,
            QuotedFare = fare,
            Fare = fare,
            SurgeMultiplier = 1m,
            PickupInstructions = posted.PickupInstructions,
            RequestedUtc = now,
            AcceptanceDeadlineUtc = now.AddSeconds(30)
        };

        driver.Availability = DriverAvailability.Dispatched;
        booking.DispatchAttempts.Add(new DispatchAttempt { DriverProfileId = driver.Id });
        db.RideBookings.Add(booking);
        await db.SaveChangesAsync(ct);

        return (new IncomingRiderRequestView(booking.Id, rider.PublicName, 4.9,
            $"{origin.Name} · {origin.Address}", passengers, fare, booking.AcceptanceDeadlineUtc!.Value, now), null);
    }

    public async Task<IncomingRiderRequestView?> FindPendingRequestAsync(int driverProfileId, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var booking = await db.RideBookings.AsNoTracking()
            .Include(ride => ride.Rider)
            .Where(ride => ride.DriverProfileId == driverProfileId &&
                           ride.Status == RideStatus.Requested &&
                           ride.AcceptanceDeadlineUtc > now)
            .OrderByDescending(ride => ride.RequestedUtc)
            .FirstOrDefaultAsync(ct);
        if (booking == null)
            return null;

        var pickup = RideRouteCatalog.View.Places
            .OrderBy(place => RideService.DistanceKm(booking.PickupLatitude, booking.PickupLongitude,
                place.Latitude, place.Longitude))
            .FirstOrDefault();
        return new IncomingRiderRequestView(booking.Id, booking.Rider.PublicName, 4.9,
            pickup == null ? "Requested pickup" : $"{pickup.Name} · {pickup.Address}",
            booking.Passengers, booking.Fare, booking.AcceptanceDeadlineUtc!.Value, now);
    }

    private async Task<ActiveBookingView?> FindActiveBookingAsync(int driverProfileId, CancellationToken ct)
    {
        var booking = await db.RideBookings.AsNoTracking()
            .Include(ride => ride.Rider)
            .Where(ride => ride.DriverProfileId == driverProfileId &&
                           (ride.Status == RideStatus.Assigned || ride.Status == RideStatus.Arrived ||
                            ride.Status == RideStatus.InProgress))
            .OrderByDescending(ride => ride.AssignedUtc)
            .FirstOrDefaultAsync(ct);
        if (booking == null)
            return null;

        var pickup = RideRouteCatalog.View.Places
            .OrderBy(place => RideService.DistanceKm(booking.PickupLatitude, booking.PickupLongitude,
                place.Latitude, place.Longitude))
            .FirstOrDefault();
        return new ActiveBookingView(booking.Id, booking.Status, booking.Rider.PublicName,
            pickup == null ? "Pickup" : $"{pickup.Name} · {pickup.Address}",
            booking.Passengers, booking.Fare, booking.PickupEtaUtc);
    }

    private async Task<Member> EnsureDemoRiderAsync(CancellationToken ct)
    {
        const string email = "demo.rider@dashlane.local";
        var existing = await db.Members.SingleOrDefaultAsync(member => member.Email == email, ct);
        if (existing != null)
            return existing;

        var rider = new Member
        {
            Email = email,
            FullName = "Alex Rider",
            LegalName = "Alex Rider",
            PhoneVerified = true,
            PhotoFileName = "demo",
            CreatedAtUtc = clock.GetUtcNow().UtcDateTime,
            OnboardingCompletedUtc = clock.GetUtcNow().UtcDateTime
        };
        db.Members.Add(rider);
        await db.SaveChangesAsync(ct);
        return rider;
    }

    private static RidePlace? MatchPlace(string address) =>
        RideRouteCatalog.View.Places.FirstOrDefault(place =>
            address.Contains(place.Name, StringComparison.OrdinalIgnoreCase) ||
            address.Contains(place.Address, StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<PostedRideStopDraft> ParseStops(string json)
    {
        try
        {
            var labels = JsonSerializer.Deserialize<string[]>(json) ?? [];
            return labels.Select((address, index) => new PostedRideStopDraft(address, null, (index + 1) * 10, index))
                .ToArray();
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
