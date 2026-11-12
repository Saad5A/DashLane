using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using DashLane.Models;
using DashLane.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace DashLane.Controllers;

public class RidesController(RideService rides, TimeProvider clock, MembershipService membership,
    IWebHostEnvironment environment) : Controller
{
    private const string DraftKey = "RideDraft";
    private static readonly TimeZoneInfo Toronto = TimeZoneInfo.FindSystemTimeZoneById("America/Toronto");

    [HttpGet]
    public IActionResult Policy() => View();

    [HttpGet]
    public new IActionResult Request(bool edit = false)
    {
        var model = RideRouteCatalog.View;
        if (edit && ReadDraft() is { } draft)
        {
            var scheduledLocal = draft.ScheduledUtc is DateTime utc
                ? TimeZoneInfo.ConvertTimeFromUtc(utc, Toronto).ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture)
                : null;
            model = model with { Initial = new RideSelection(draft.Pickup.Id, draft.Destination.Id,
                draft.Quote.Tier, draft.Passengers, draft.RequiresWheelchairAccess, draft.Quote.Fare,
                draft.Quote.SurgeMultiplier, scheduledLocal) };
            Response.Headers.CacheControl = "no-store";
        }

        return View(model);
    }

    [HttpPost]
    [ActionName(nameof(Request))]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveRequest([FromBody] RideSelection? selection, CancellationToken ct)
    {
        if (!ModelState.IsValid || selection == null || selection.Passengers is < 1 or > 6 ||
            RideRouteCatalog.Find(selection.PickupId, selection.DestinationId) is not { } route)
            return BadRequest(new { message = "Choose two different stops and a valid passenger count." });

        var now = clock.GetUtcNow();
        DateTime? scheduledUtc = null;
        if (!string.IsNullOrEmpty(selection.ScheduledLocal))
        {
            if (!DateTime.TryParseExact(selection.ScheduledLocal, "yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var local) || Toronto.IsInvalidTime(local) || Toronto.IsAmbiguousTime(local))
                return BadRequest(new { message = "Choose a valid, unambiguous Eastern time." });

            scheduledUtc = TimeZoneInfo.ConvertTimeToUtc(local, Toronto);
            if (scheduledUtc <= now.UtcDateTime)
                return BadRequest(new { message = "Choose a departure time in the future." });
        }

        var input = RideRouteCatalog.QuoteRequest(route, selection.Passengers, selection.RequiresWheelchairAccess);
        var quote = (await rides.QuoteAsync(input, ct)).SingleOrDefault(fare => fare.Tier == selection.Tier);
        if (quote == null || quote.Fare != selection.Fare || quote.SurgeMultiplier != selection.SurgeMultiplier)
            return Conflict(new { message = "Your quote changed. Review the updated fare before continuing." });

        var places = RideRouteCatalog.View.Places;
        var draft = new RideDraft(places.Single(place => place.Id == route.PickupId),
            places.Single(place => place.Id == route.DestinationId), route, quote, selection.Passengers,
            selection.RequiresWheelchairAccess, scheduledUtc, now) { Id = Guid.NewGuid() };

        TempData[DraftKey] = JsonSerializer.Serialize(draft);
        return Ok(new { redirectUrl = Url.Action(nameof(Confirm)) });
    }

    [HttpGet]
    public async Task<IActionResult> Confirm(CancellationToken ct)
    {
        if (ReadDraft() is not { } draft)
            return RedirectToAction(nameof(Request));

        Response.Headers.CacheControl = "no-store";
        return View(await ConfirmModelAsync(draft, draft.Confirmation, ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(DriverConfirmInput input, CancellationToken ct)
    {
        if (ReadDraft() is not { } draft || input.DraftId != draft.Id)
            return RedirectToAction(nameof(Confirm));

        var model = await ConfirmModelAsync(draft, input, ct);
        if (!ModelState.IsValid || !model.Drivers.Any(driver => driver.Id == input.DriverId))
        {
            Response.Headers.CacheControl = "no-store";
            return View(model with { Error = "Choose an available driver and keep each note to 200 characters." });
        }

        // The fare and route come exclusively from the protected Stage 2 draft.
        TempData[DraftKey] = JsonSerializer.Serialize(draft with
        {
            Confirmation = new DriverConfirmInput
            {
                DraftId = draft.Id, DriverId = input.DriverId,
                PickupInstructions = input.PickupInstructions?.Trim(), DriverNote = input.DriverNote?.Trim(),
                DriverName = model.Drivers.Single(driver => driver.Id == input.DriverId).Name,
                Vehicle = model.Drivers.Where(driver => driver.Id == input.DriverId)
                    .Select(driver => driver.Make + " " + driver.Model).Single()
            }
        });
        return RedirectToAction(nameof(Checkout));
    }

    [HttpGet]
    public IActionResult Checkout()
    {
        if (ReadDraft() is not { Confirmation: not null } draft)
            return RedirectToAction(nameof(Confirm));

        Response.Headers.CacheControl = "no-store";
        var signedIn = User.Identity?.IsAuthenticated == true;
        var notice = draft.Confirmation.DriverId is not > 0 ? "Choose a live driver to submit a payment."
            : draft.ScheduledUtc > clock.GetUtcNow().UtcDateTime ? "Scheduled rides can be previewed. Test checkout supports immediate departures."
            : !signedIn ? "Sign in to submit a test payment." : null;
        return View(new CheckoutViewModel(draft, CheckoutFare.From(draft), notice == null, notice));
    }

    [Authorize, HttpPost, ActionName(nameof(Checkout)), ValidateAntiForgeryToken]
    public async Task<IActionResult> Pay([FromBody] CheckoutInput? input, CancellationToken ct)
    {
        if (!ModelState.IsValid || input == null)
            return BadRequest(new { message = "Choose a valid test payment method." });
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var riderId) ||
            await membership.FindAsync(riderId) == null)
            return Unauthorized();
        if (await rides.RiderBookingAsync(input.DraftId, riderId, ct) != null)
            return Ok(new { redirectUrl = Url.Action(nameof(Status), new { id = input.DraftId }) });
        if (ReadDraft() is not { Confirmation: not null } draft || draft.Id != input.DraftId)
            return Conflict(new { message = "This ride draft has changed. Reopen checkout to continue." });

        var result = await rides.CheckoutAsync(draft, riderId, input, ct);
        if (result.BookingId is not Guid id)
            return Conflict(new { message = result.Error });

        TempData.Remove(DraftKey);
        return Ok(new { redirectUrl = Url.Action(nameof(Status), new { id }) });
    }

    [Authorize, HttpGet("/Rides/Status/{id:guid}")]
    public async Task<IActionResult> Status(Guid id, CancellationToken ct)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var riderId) ||
            await rides.TripAsync(id, riderId, environment.IsDevelopment(), ct) is not { } trip)
            return NotFound();

        Response.Headers.CacheControl = "no-store";
        return View(trip);
    }

    [Authorize, HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> DriverApproach(Guid draftId, CancellationToken ct)
    {
        if (ReadDraft() is not { } draft || draft.Id != draftId)
            return NotFound();

        var drivers = await LiveDriversAsync(draft, ct);
        return Json(drivers.Select(driver => Approach(draft, driver.Id, driver.Latitude, driver.Longitude)));
    }

    [Authorize, HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> DriverPhoto(int id, CancellationToken ct)
    {
        if (ReadDraft() is not { } draft)
            return NotFound();

        var driver = (await LiveDriversAsync(draft, ct)).SingleOrDefault(driver => driver.Id == id);
        var path = driver == null ? null : membership.MemberFilePath(driver.Member, driver.Member.PhotoFileName);
        var contentType = Path.GetExtension(path)?.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => null
        };
        return path != null && contentType != null && System.IO.File.Exists(path)
            ? PhysicalFile(path, contentType) : NotFound();
    }

    private Task<IReadOnlyList<DriverProfile>> LiveDriversAsync(RideDraft draft, CancellationToken ct) =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var memberId)
            ? rides.EligibleDriversAsync(draft.Pickup.Latitude, draft.Pickup.Longitude, draft.Quote.Tier,
                draft.Passengers, draft.RequiresWheelchairAccess, memberId, ct)
            : Task.FromResult<IReadOnlyList<DriverProfile>>([]);

    private async Task<DriverConfirmViewModel> ConfirmModelAsync(RideDraft draft, DriverConfirmInput? input,
        CancellationToken ct)
    {
        var matches = await LiveDriversAsync(draft, ct);
        var preview = matches.Count == 0;
        IReadOnlyList<DriverOption> options;
        if (preview)
        {
            var (make, model, capacity, accessible) = draft.Quote.Tier switch
            {
                VehicleTier.XL => ("Toyota", "Highlander", 6, false),
                VehicleTier.Comfort => ("Lexus", "ES", 4, false),
                VehicleTier.WheelchairAccessible => ("Toyota", "Sienna", 4, true),
                _ => ("Toyota", "Camry", 4, false)
            };
            options =
            [
                new(-1, "Marcus Chen", new(4.96, 248), Url.Content("~/images/avatars/marcus.jpg"),
                    make, model, 2023, "DEMO 01", capacity, accessible,
                    Approach(draft, -1, draft.Pickup.Latitude + .019, draft.Pickup.Longitude - .023)),
                new(-2, "Elena Rostova", new(4.93, 186), Url.Content("~/images/avatars/elena-rostova.jpg"),
                    make, model, 2024, "DEMO 02", capacity, accessible,
                    Approach(draft, -2, draft.Pickup.Latitude + .037, draft.Pickup.Longitude + .025))
            ];
        }
        else
        {
            var ratings = await rides.RatingsAsync(matches.Select(driver => driver.Id).ToArray(), ct);
            options = matches.Select(driver => new DriverOption(driver.Id,
                string.IsNullOrWhiteSpace(driver.Member.FullName) ? driver.Member.PublicName : driver.Member.FullName,
                ratings.GetValueOrDefault(driver.Id),
                driver.Member.PhotoFileName == null ? null : Url.Action(nameof(DriverPhoto), new { driver.Id }),
                driver.Vehicle.Make, driver.Vehicle.Model, driver.Vehicle.Year, driver.Vehicle.Plate,
                driver.Vehicle.PassengerCapacity, driver.Vehicle.WheelchairAccessible,
                Approach(draft, driver.Id, driver.Latitude, driver.Longitude))).ToList();
        }

        var selected = options.FirstOrDefault(driver => driver.Id == input?.DriverId);
        return new DriverConfirmViewModel(draft, options, new DriverConfirmInput
        {
            DraftId = draft.Id,
            DriverId = selected?.Id ?? options[0].Id,
            PickupInstructions = input?.PickupInstructions,
            DriverNote = selected == null ? null : input?.DriverNote
        }, preview);
    }

    private static DriverApproach Approach(RideDraft draft, int id, double latitude, double longitude)
    {
        var distance = RideService.DistanceKm(draft.Pickup.Latitude, draft.Pickup.Longitude, latitude, longitude);
        return new DriverApproach(id, new RideMapPoint(Math.Round(latitude, 2), Math.Round(longitude, 2)),
            Math.Max(2, (int)Math.Ceiling(distance * 2.4)));
    }

    private RideDraft? ReadDraft() => ReadDraft(TempData);

    internal static RideDraft? ReadDraft(ITempDataDictionary tempData) => tempData.Peek(DraftKey) is string json
        ? JsonSerializer.Deserialize<RideDraft>(json)
        : null;
}
