using System.Security.Claims;
using System.Text.Json;
using DashLane.Models;
using DashLane.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DashLane.Controllers;

[Authorize]
public class DriverController(DriverService drivers, MembershipService membership, TimeProvider clock) : Controller
{
    private const string DraftKey = "PostedRideDraft";
    private static readonly TimeZoneInfo Toronto = TimeZoneInfo.FindSystemTimeZoneById("America/Toronto");
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [HttpGet]
    public IActionResult Post() => RedirectToAction(nameof(Onboarding));

    [HttpGet]
    public async Task<IActionResult> Onboarding(CancellationToken ct)
    {
        if (await CurrentMemberIdAsync() is not int memberId)
            return Challenge();

        var model = await drivers.BuildOnboardingAsync(memberId, ct);
        return model == null ? Challenge() : View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Onboarding(DriverOnboardingViewModel input, CancellationToken ct)
    {
        if (await CurrentMemberIdAsync() is not int memberId)
            return Challenge();

        var display = await drivers.BuildOnboardingAsync(memberId, ct) ?? input;
        input.PhoneVerified = display.PhoneVerified;
        input.ProfilePhotoUploaded = display.ProfilePhotoUploaded;
        input.RequiresLicenseVerification = display.RequiresLicenseVerification;
        input.LicenseVerified = display.LicenseVerified;
        input.BackgroundCheckVerified = display.BackgroundCheckVerified;
        input.HasExistingVehicle = display.HasExistingVehicle;
        input.ExistingLicenseDocumentUrl = display.ExistingLicenseDocumentUrl;

        if (!ModelState.IsValid)
        {
            input.Error = "Check the highlighted vehicle details and try again.";
            return View(input);
        }

        var (ok, error) = await drivers.SaveOnboardingAsync(memberId, input, ct);
        if (!ok)
        {
            input.Error = error;
            return View(input);
        }

        return RedirectToAction(nameof(PostRoute));
    }

    [HttpGet]
    public async Task<IActionResult> PostRoute(CancellationToken ct)
    {
        if (await CurrentMemberIdAsync() is not int memberId)
            return Challenge();
        if (await drivers.FindByMemberAsync(memberId, ct) is not { Vehicle: not null })
            return RedirectToAction(nameof(Onboarding));

        ViewData["Step"] = 2;
        return View(BuildPostRouteModel(ReadDraft()));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PostRoute(PostRouteViewModel input, CancellationToken ct)
    {
        if (await CurrentMemberIdAsync() is not int memberId)
            return Challenge();
        if (await drivers.FindByMemberAsync(memberId, ct) is not { Vehicle: not null })
            return RedirectToAction(nameof(Onboarding));

        ViewData["Step"] = 2;
        input.Places = RideRouteCatalog.View.Places;
        input.Routes = RideRouteCatalog.View.Routes;
        MergeStopsFromJson(input);

        if (string.IsNullOrWhiteSpace(input.OriginPlaceId) || string.IsNullOrWhiteSpace(input.DestinationPlaceId))
        {
            input.Error = "Choose an origin and destination.";
            return View(input);
        }

        if (input.OriginPlaceId == input.DestinationPlaceId)
        {
            input.Error = "Origin and destination must be different stops.";
            return View(input);
        }

        var corridor = RideRouteCatalog.Find(input.OriginPlaceId, input.DestinationPlaceId);
        if (corridor == null)
        {
            input.Error = "Choose two supported stops with a known corridor.";
            return View(input);
        }

        var places = RideRouteCatalog.View.Places.ToDictionary(place => place.Id);
        if (!places.TryGetValue(input.OriginPlaceId, out var origin) ||
            !places.TryGetValue(input.DestinationPlaceId, out var destination))
        {
            input.Error = "Choose valid catalog stops.";
            return View(input);
        }

        var stops = NormalizeStops(input.Stops, input.OriginPlaceId, input.DestinationPlaceId);
        input.Stops = stops;
        var metrics = MeasureRoute(origin.Id, destination.Id, stops, corridor);
        input.DistanceKm = metrics.DistanceKm;
        input.DurationMin = metrics.DurationMin;

        if (input.DepartureDate is not DateOnly date || input.DepartureTime is not TimeOnly time)
        {
            input.Error = "Choose a departure date and time.";
            return View(input);
        }

        var local = date.ToDateTime(time);
        if (Toronto.IsInvalidTime(local) || Toronto.IsAmbiguousTime(local))
        {
            input.Error = "Choose a valid, unambiguous Eastern departure time.";
            return View(input);
        }

        var departureUtc = TimeZoneInfo.ConvertTimeToUtc(local, Toronto);
        if (departureUtc <= clock.GetUtcNow().UtcDateTime)
        {
            input.Error = "Choose a departure time in the future.";
            return View(input);
        }

        var previous = ReadDraft();
        var draft = new PostedRideDraft(
            origin.Id,
            destination.Id,
            $"{origin.Name} · {origin.Address}",
            $"{destination.Name} · {destination.Address}",
            departureUtc,
            metrics.DistanceKm,
            metrics.DurationMin,
            stops.Select((stop, index) => new PostedRideStopDraft(
                stop.Address.Trim(),
                string.IsNullOrWhiteSpace(stop.PlaceId) ? null : stop.PlaceId,
                stop.ArrivalOffsetMin,
                index)).ToArray(),
            previous?.PickupInstructions,
            previous?.LuggageAllowance,
            previous?.AvailableSeats ?? 1,
            previous?.AutoApproveRiders ?? true,
            previous?.EstimatedPricePerSeat ?? 0m,
            previous?.EstimatedTotalEarnings ?? 0m,
            previous?.Tier);

        TempData[DraftKey] = JsonSerializer.Serialize(draft);
        return RedirectToAction(nameof(PostDetails));
    }

    [HttpGet]
    public async Task<IActionResult> PostDetails(CancellationToken ct)
    {
        if (await CurrentMemberIdAsync() is not int memberId)
            return Challenge();
        if (ReadDraft() is not { } draft)
            return RedirectToAction(nameof(PostRoute));
        if (await drivers.FindByMemberAsync(memberId, ct) is not { Vehicle: not null } profile)
            return RedirectToAction(nameof(Onboarding));

        ViewData["Step"] = 3;
        return View(BuildPostDetailsModel(draft, profile));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PostDetails(PostDetailsViewModel input, CancellationToken ct)
    {
        if (await CurrentMemberIdAsync() is not int memberId)
            return Challenge();
        if (ReadDraft() is not { } draft)
            return RedirectToAction(nameof(PostRoute));
        if (await drivers.FindByMemberAsync(memberId, ct) is not { Vehicle: not null } profile)
            return RedirectToAction(nameof(Onboarding));

        ViewData["Step"] = 3;
        var model = BuildPostDetailsModel(draft, profile);
        model.PickupInstructions = input.PickupInstructions;
        model.LuggageAllowance = input.LuggageAllowance;
        model.AvailableSeats = input.AvailableSeats;
        model.AutoApproveRiders = input.AutoApproveRiders;

        if (!LuggageOptions.All.Contains(input.LuggageAllowance))
        {
            model.Error = "Choose a valid luggage allowance.";
            return View(model);
        }

        if (input.AvailableSeats is < 1 || input.AvailableSeats > profile.Vehicle.PassengerCapacity)
        {
            model.Error = $"Available seats must be between 1 and {profile.Vehicle.PassengerCapacity}.";
            return View(model);
        }

        EstimatePayoutResult payout;
        try
        {
            payout = drivers.EstimatePayout(new EstimatePayoutRequest
            {
                Tier = profile.Vehicle.Tier,
                DistanceKm = draft.DistanceKm,
                DurationMin = draft.DurationMin,
                AvailableSeats = input.AvailableSeats
            });
        }
        catch (ArgumentOutOfRangeException ex)
        {
            model.Error = ex.Message;
            return View(model);
        }

        var updated = draft with
        {
            PickupInstructions = string.IsNullOrWhiteSpace(input.PickupInstructions)
                ? null : input.PickupInstructions.Trim(),
            LuggageAllowance = input.LuggageAllowance,
            AvailableSeats = input.AvailableSeats,
            AutoApproveRiders = input.AutoApproveRiders,
            EstimatedPricePerSeat = payout.EstimatedPricePerSeat,
            EstimatedTotalEarnings = payout.EstimatedTotalEarnings,
            Tier = profile.Vehicle.Tier
        };
        TempData[DraftKey] = JsonSerializer.Serialize(updated);
        return RedirectToAction(nameof(Publish));
    }

    [HttpGet]
    public async Task<IActionResult> Publish(CancellationToken ct)
    {
        if (await CurrentMemberIdAsync() is not int memberId)
            return Challenge();
        if (ReadDraft() is not { } draft || draft.EstimatedPricePerSeat <= 0)
            return RedirectToAction(nameof(PostDetails));
        if (await drivers.FindByMemberAsync(memberId, ct) is not { Vehicle: not null } profile)
            return RedirectToAction(nameof(Onboarding));

        ViewData["Step"] = 4;
        return View(BuildPublishModel(draft, profile));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Publish(PublishRideViewModel input, CancellationToken ct)
    {
        if (await CurrentMemberIdAsync() is not int memberId)
            return Challenge();
        if (ReadDraft() is not { } draft || draft.EstimatedPricePerSeat <= 0)
            return RedirectToAction(nameof(PostDetails));
        if (await drivers.FindByMemberAsync(memberId, ct) is not { Vehicle: not null } profile)
            return RedirectToAction(nameof(Onboarding));

        ViewData["Step"] = 4;
        var model = BuildPublishModel(draft, profile);
        model.AcceptPunctualityPolicy = input.AcceptPunctualityPolicy;

        if (!input.AcceptPunctualityPolicy)
        {
            model.Error = "Accept DashLane's Driver Punctuality & Cancellation Policy to publish.";
            return View(model);
        }

        var (result, error) = await drivers.PublishDraftAsync(memberId, draft, input.AcceptPunctualityPolicy, ct);
        if (error != null || result == null)
        {
            model.Error = error ?? "Unable to publish this ride.";
            return View(model);
        }

        TempData.Remove(DraftKey);
        return RedirectToAction(nameof(ActiveTrip), new { id = result.Id });
    }

    [HttpGet]
    public async Task<IActionResult> ActiveTrip(Guid id, CancellationToken ct)
    {
        if (await CurrentMemberIdAsync() is not int memberId)
            return Challenge();

        var model = await drivers.BuildActiveTripAsync(id, memberId, ct);
        if (model == null)
            return RedirectToAction(nameof(Onboarding));

        ViewData["Step"] = 4;
        return View(model);
    }

    private static PublishRideViewModel BuildPublishModel(PostedRideDraft draft, DriverProfile profile) =>
        new()
        {
            OriginAddress = draft.OriginAddress,
            DestinationAddress = draft.DestinationAddress,
            DepartureUtc = draft.DepartureUtc,
            DistanceKm = draft.DistanceKm,
            DurationMin = draft.DurationMin,
            Stops = draft.Stops.OrderBy(stop => stop.Order).ToArray(),
            PickupInstructions = draft.PickupInstructions,
            LuggageAllowance = draft.LuggageAllowance,
            AvailableSeats = draft.AvailableSeats,
            AutoApproveRiders = draft.AutoApproveRiders,
            EstimatedPricePerSeat = draft.EstimatedPricePerSeat,
            EstimatedTotalEarnings = draft.EstimatedTotalEarnings,
            Tier = draft.Tier ?? profile.Vehicle.Tier,
            DriverName = profile.Member.PublicName,
            LicenseVerified = profile.LicenseVerified,
            VehicleLabel = $"{profile.Vehicle.Year} {profile.Vehicle.Make} {profile.Vehicle.Model}".Trim(),
            LicensePlate = profile.Vehicle.Plate,
            OriginPlaceId = draft.OriginPlaceId,
            DestinationPlaceId = draft.DestinationPlaceId
        };

    private static PostDetailsViewModel BuildPostDetailsModel(PostedRideDraft draft, DriverProfile profile)
    {
        var capacity = Math.Clamp(profile.Vehicle.PassengerCapacity, 1, 6);
        var seats = draft.AvailableSeats > 0
            ? Math.Clamp(draft.AvailableSeats, 1, capacity)
            : Math.Min(capacity, 2);

        return new PostDetailsViewModel
        {
            PickupInstructions = draft.PickupInstructions,
            LuggageAllowance = string.IsNullOrWhiteSpace(draft.LuggageAllowance)
                ? "Carry-On" : draft.LuggageAllowance,
            AvailableSeats = seats,
            AutoApproveRiders = draft.AutoApproveRiders,
            EstimatedPricePerSeat = draft.EstimatedPricePerSeat,
            EstimatedTotalEarnings = draft.EstimatedTotalEarnings,
            MaxSeats = capacity,
            Tier = profile.Vehicle.Tier,
            DistanceKm = draft.DistanceKm,
            DurationMin = draft.DurationMin,
            OriginAddress = draft.OriginAddress,
            DestinationAddress = draft.DestinationAddress,
            DepartureUtc = draft.DepartureUtc,
            StopCount = draft.Stops.Count
        };
    }

    private PostRouteViewModel BuildPostRouteModel(PostedRideDraft? draft)
    {
        var model = new PostRouteViewModel
        {
            Places = RideRouteCatalog.View.Places,
            Routes = RideRouteCatalog.View.Routes
        };

        if (draft == null)
            return model;

        model.OriginPlaceId = draft.OriginPlaceId;
        model.DestinationPlaceId = draft.DestinationPlaceId;
        model.DistanceKm = draft.DistanceKm;
        model.DurationMin = draft.DurationMin;
        model.Stops = draft.Stops
            .OrderBy(stop => stop.Order)
            .Select(stop => new PostRouteStopInput
            {
                Address = stop.Address,
                PlaceId = stop.PlaceId,
                ArrivalOffsetMin = stop.ArrivalOffsetMin
            }).ToList();

        var local = TimeZoneInfo.ConvertTimeFromUtc(draft.DepartureUtc, Toronto);
        model.DepartureDate = DateOnly.FromDateTime(local);
        model.DepartureTime = TimeOnly.FromDateTime(local);
        return model;
    }

    private static void MergeStopsFromJson(PostRouteViewModel input)
    {
        if (input.Stops.Count > 0 || string.IsNullOrWhiteSpace(input.StopsJson))
            return;

        try
        {
            var parsed = JsonSerializer.Deserialize<List<PostRouteStopInput>>(input.StopsJson, JsonOptions);
            if (parsed != null)
                input.Stops = parsed;
        }
        catch (JsonException)
        {
            // Keep empty stops; validation will surface route errors.
        }
    }

    private static List<PostRouteStopInput> NormalizeStops(
        IEnumerable<PostRouteStopInput>? stops, string originId, string destinationId)
    {
        var places = RideRouteCatalog.View.Places.ToDictionary(place => place.Id);
        var cleaned = new List<PostRouteStopInput>();
        foreach (var stop in stops ?? [])
        {
            var address = stop.Address?.Trim() ?? "";
            var placeId = string.IsNullOrWhiteSpace(stop.PlaceId) ? null : stop.PlaceId.Trim();
            if (placeId != null && places.TryGetValue(placeId, out var place))
                address = $"{place.Name} · {place.Address}";
            else
                placeId = places.Values.FirstOrDefault(place =>
                    string.Equals($"{place.Name} · {place.Address}", address, StringComparison.OrdinalIgnoreCase))?.Id;

            if (string.IsNullOrWhiteSpace(address))
                continue;
            if (placeId is not null && (placeId == originId || placeId == destinationId))
                continue;

            cleaned.Add(new PostRouteStopInput
            {
                Address = address,
                PlaceId = placeId,
                ArrivalOffsetMin = Math.Clamp(stop.ArrivalOffsetMin <= 0 ? 10 : stop.ArrivalOffsetMin, 1, 600)
            });
        }

        return cleaned.Take(5).ToList();
    }

    private static (decimal DistanceKm, decimal DurationMin) MeasureRoute(
        string originId, string destinationId, IReadOnlyList<PostRouteStopInput> stops, RidePreview corridor)
    {
        var ids = new List<string> { originId };
        ids.AddRange(stops.Select(stop => stop.PlaceId).Where(id => !string.IsNullOrWhiteSpace(id))!);
        ids.Add(destinationId);

        decimal distance = 0;
        decimal duration = 0;
        var chained = true;
        for (var i = 0; i < ids.Count - 1; i++)
        {
            var segment = RideRouteCatalog.Find(ids[i], ids[i + 1]);
            if (segment == null)
            {
                chained = false;
                break;
            }

            distance += segment.DistanceKm;
            duration += segment.DurationMin;
        }

        if (chained && ids.Count > 2)
            return (FareSchedule.Money(distance), duration);

        var unknownStops = stops.Count(stop => string.IsNullOrWhiteSpace(stop.PlaceId));
        var knownExtra = Math.Max(0, stops.Count - unknownStops);
        return (
            FareSchedule.Money(corridor.DistanceKm + knownExtra * 3.5m + unknownStops * 4m),
            corridor.DurationMin + stops.Sum(stop => Math.Max(5, stop.ArrivalOffsetMin / 2)) + unknownStops * 4);
    }

    private PostedRideDraft? ReadDraft() => TempData.Peek(DraftKey) is string json
        ? JsonSerializer.Deserialize<PostedRideDraft>(json)
        : null;

    private async Task<int?> CurrentMemberIdAsync()
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var memberId))
            return null;
        return await membership.FindAsync(memberId) == null ? null : memberId;
    }
}
