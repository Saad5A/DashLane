using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using DashLane.Models;
using DashLane.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace DashLane.Controllers;

[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class RidesController(RideService rides, TimeProvider clock, MembershipService membership,
    IWebHostEnvironment environment) : Controller
{
    private const string DraftKey = "RideDraft";
    private static readonly TimeZoneInfo Toronto = TimeZoneInfo.FindSystemTimeZoneById("America/Toronto");

    [AllowAnonymous, HttpGet]
    public async Task<IActionResult> Policy(CancellationToken ct) => View(await rides.FareRatesAsync(ct));

    [AllowAnonymous, HttpGet]
    public new async Task<IActionResult> Request(bool edit = false, VehicleTier? tier = null, CancellationToken ct = default)
    {
        var form = new RideRequestForm();
        if (tier is { } selectedTier && Enum.IsDefined(selectedTier))
        {
            form.Tier = selectedTier;
            form.RequiresWheelchairAccess = selectedTier == VehicleTier.WheelchairAccessible;
        }
        if (edit && ReadDraft() is { } draft)
        {
            form = new RideRequestForm { PickupId = draft.Pickup.Id, DestinationId = draft.Destination.Id,
                Tier = draft.Quote.Tier, Passengers = draft.Passengers, RequiresWheelchairAccess = draft.RequiresWheelchairAccess,
                ScheduledLocal = draft.ScheduledUtc is DateTime utc
                    ? TimeZoneInfo.ConvertTimeFromUtc(utc, Toronto).ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture) : null };
        }
        ViewData["LiveRideQuotes"] = await QuoteCatalogAsync(null, null, ct);
        return View(RideRouteCatalog.View with { Form = form });
    }

    // Quotes use the same persisted rates, demand and corridor measurements as checkout.
    [AllowAnonymous, HttpGet]
    public async Task<IActionResult> Quotes(string pickupId, string destinationId, CancellationToken ct)
    {
        if (RideRouteCatalog.Find(pickupId, destinationId) == null) return BadRequest();
        return Json(await QuoteCatalogAsync(pickupId, destinationId, ct));
    }

    private async Task<object> QuoteCatalogAsync(string? pickupId, string? destinationId, CancellationToken ct)
    {
        var result = new List<object>();
        var rates = await rides.FareRatesAsync(ct);
        foreach (var route in RideRouteCatalog.View.Routes.Where(r => pickupId == null ||
                     (r.PickupId == pickupId && r.DestinationId == destinationId)))
        {
            var fares = await rides.QuoteAsync(RideRouteCatalog.QuoteRequest(route, 1, false), ct);
            result.Add(new { route.PickupId, route.DestinationId, route.DistanceKm, route.DurationMin,
                route.Points, Fares = fares.Select(f => new { Tier = f.Tier.ToString(), f.Fare,
                    f.SurgeMultiplier, Capacity = rates.Single(r => r.Tier == f.Tier).Capacity }) });
        }
        return result;
    }

    [AllowAnonymous, HttpPost, ActionName(nameof(Request)), ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveRequest([Bind(Prefix = "Form")] RideRequestForm form, string intent, CancellationToken ct)
    {
        if (form.RequiresWheelchairAccess || form.Tier == VehicleTier.WheelchairAccessible)
        {
            form.Tier = VehicleTier.WheelchairAccessible;
            form.RequiresWheelchairAccess = true;
            ModelState.Remove("Form.Tier");
            ModelState.Remove("Form.RequiresWheelchairAccess");
        }
        ViewData["LiveRideQuotes"] = await QuoteCatalogAsync(null, null, ct);
        var route = RideRouteCatalog.Find(form.PickupId, form.DestinationId);
        if (route == null) ModelState.AddModelError("Form.DestinationId", "Choose two different supported stops.");
        DateTime? scheduled = null;
        if (!string.IsNullOrWhiteSpace(form.ScheduledLocal))
        {
            if (!DateTime.TryParseExact(form.ScheduledLocal, "yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var local) || Toronto.IsInvalidTime(local) || Toronto.IsAmbiguousTime(local))
                ModelState.AddModelError("Form.ScheduledLocal", "Choose a valid, unambiguous Eastern time.");
            else if ((scheduled = TimeZoneInfo.ConvertTimeToUtc(local, Toronto)) <= clock.GetUtcNow().UtcDateTime)
                ModelState.AddModelError("Form.ScheduledLocal", "Choose a departure in the future.");
        }
        if (!ModelState.IsValid || route == null) return View("Request", RideRouteCatalog.View with { Form = form });
        var quotes = await rides.QuoteAsync(RideRouteCatalog.QuoteRequest(route, form.Passengers, form.RequiresWheelchairAccess), ct);
        var quote = quotes.SingleOrDefault(q => q.Tier == form.Tier);
        if (quote == null) ModelState.AddModelError("Form.Tier", "Choose a tier that supports your passenger and accessibility needs.");
        if (intent == "continue" && quote != null && form.ExpectedFare == quote.Fare && form.ExpectedSurge == quote.SurgeMultiplier)
        {
            var places = RideRouteCatalog.View.Places;
            var draft = new RideDraft(places.Single(p => p.Id == route.PickupId), places.Single(p => p.Id == route.DestinationId),
                route, quote, form.Passengers, form.RequiresWheelchairAccess, scheduled, clock.GetUtcNow()) { Id = Guid.NewGuid() };
            TempData[DraftKey] = JsonSerializer.Serialize(draft);
            return RedirectToAction(nameof(Confirm));
        }
        if (intent == "continue" && quote != null)
            ModelState.AddModelError("", "Review the current fare below, then continue to confirm it.");
        form.ExpectedFare = quote?.Fare; form.ExpectedSurge = quote?.SurgeMultiplier;
        ModelState.Remove("Form.ExpectedFare"); ModelState.Remove("Form.ExpectedSurge");
        return View("Request", RideRouteCatalog.View with { Form = form, Quotes = quotes, Preview = route });
    }

    [HttpGet]
    public async Task<IActionResult> Confirm(CancellationToken ct)
    {
        if (ReadDraft() is not { } draft) return RedirectToAction(nameof(Request));
        var saved = draft.Confirmation;
        return View(await ConfirmModelAsync(draft, saved == null ? null : new RideDriverSelectionInput {
            DraftId = saved.DraftId, DriverId = saved.DriverId,
            PickupInstructions = saved.PickupInstructions, DriverNote = saved.DriverNote }, ct));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm([Bind(Prefix = "Input")] RideDriverSelectionInput input, CancellationToken ct)
    {
        if (ReadDraft() is not { } draft || input.DraftId != draft.Id) return RedirectToAction(nameof(Confirm));
        var model = await ConfirmModelAsync(draft, input, ct);
        if (!ModelState.IsValid || !model.Drivers.Any(d => d.Id == input.DriverId))
        {
            ModelState.AddModelError("", "Choose an available driver and keep each note to 200 characters.");
            return View(model);
        }
        var selected = model.Drivers.Single(d => d.Id == input.DriverId);
        TempData[DraftKey] = JsonSerializer.Serialize(draft with { Confirmation = new RideConfirmation
        {
            DraftId = draft.Id, DriverId = selected.Id, DriverName = selected.Name,
            Vehicle = selected.Make + " " + selected.Model,
            PickupInstructions = input.PickupInstructions?.Trim(), DriverNote = input.DriverNote?.Trim()
        }});
        return RedirectToAction(nameof(Checkout));
    }

    [HttpGet]
    public IActionResult Checkout()
    {
        if (ReadDraft() is not { Confirmation: not null } draft) return RedirectToAction(nameof(Confirm));
        return View(CheckoutModel(draft));
    }

    [HttpPost, ActionName(nameof(Checkout)), ValidateAntiForgeryToken]
    public async Task<IActionResult> Pay([Bind(Prefix = "Input")] CheckoutInput input, string intent, CancellationToken ct)
    {
        if (await MemberIdAsync() is not int riderId) return Challenge();
        if (await rides.RiderBookingAsync(input.DraftId, riderId, ct) != null)
            return RedirectToAction(nameof(Status), new { id = input.DraftId });
        if (ReadDraft() is not { Confirmation: not null } draft || input.DraftId != draft.Id)
            return RedirectToAction(nameof(Confirm));
        if (!ModelState.IsValid) return View("Checkout", CheckoutModel(draft, input));
        if (intent == "promo")
        {
            var result = string.IsNullOrWhiteSpace(input.PromoCode) ? (null, CheckoutFare.From(draft), (string?)null)
                : await rides.CheckoutPromoAsync(draft, riderId, input.PromoCode, ct);
            if (result.Item3 != null) ModelState.AddModelError("Input.PromoCode", result.Item3);
            var updated = input with { ExpectedTotal = result.Item2.Total };
            ModelState.Remove("Input.ExpectedTotal");
            return View("Checkout", CheckoutModel(draft, updated) with { Fare = result.Item2 });
        }
        var payment = await rides.CheckoutAsync(draft, riderId, input, ct);
        if (payment.BookingId is not Guid id)
        {
            ModelState.AddModelError("", payment.Error ?? "Payment could not be completed.");
            var currentFare = CheckoutFare.From(draft);
            if (!string.IsNullOrWhiteSpace(input.PromoCode))
            {
                var promotion = await rides.CheckoutPromoAsync(draft, riderId, input.PromoCode, ct);
                currentFare = promotion.Fare;
            }
            ModelState.Remove("Input.ExpectedTotal");
            return View("Checkout", CheckoutModel(draft, input with { ExpectedTotal = currentFare.Total }) with { Fare = currentFare });
        }
        TempData.Remove(DraftKey);
        return RedirectToAction(nameof(Status), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Status(Guid id, CancellationToken ct, bool autoRefresh = false)
    {
        if (await MemberIdAsync() is not int memberId) return Challenge();
        var trip = await rides.TripAsync(id, memberId, environment.IsDevelopment(), ct);
        ViewData["AutoRefresh"] = autoRefresh && trip?.Status is not ("Completed" or "Cancelled");
        return trip == null ? NotFound() : View(trip);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Cancel(Guid id, TripCancelInput input, CancellationToken ct) =>
        TripPost(id, member => rides.CancelTripAsync(id, member, input.ExpectedFee, ct), ct);
    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Advance(Guid id, TripAdvanceInput input, CancellationToken ct) =>
        TripPost(id, member => rides.AdvanceTripAsync(id, member, input.ExpectedStatus, environment.IsDevelopment(), ct), ct);
    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Chat(Guid id, TripTextInput input, CancellationToken ct) =>
        TripPost(id, member => rides.ChatAsync(id, member, input.Text, ct), ct);
    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Incident(Guid id, TripTextInput input, CancellationToken ct) =>
        TripPost(id, member => rides.IncidentAsync(id, member, input.Text, false, ct), ct);
    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Sos(Guid id, CancellationToken ct) =>
        TripPost(id, member => rides.IncidentAsync(id, member, "Simulated emergency contact notification", true, ct), ct);
    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Review(Guid id, TripReviewInput input, CancellationToken ct) =>
        TripPost(id, member => rides.ReviewTripAsync(id, member, input, ct), ct);
    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Location(Guid id, TripLocationInput input, CancellationToken ct) =>
        TripPost(id, member => rides.UpdateTripLocationAsync(id, member, input, ct), ct);

    [HttpGet]
    public async Task<IActionResult> Receipt(Guid id, bool download, CancellationToken ct)
    {
        if (await MemberIdAsync() is not int memberId) return Challenge();
        var trip = await rides.TripAsync(id, memberId, false, ct);
        if (trip == null || trip.IsDriver || trip.Paid == null) return NotFound();
        if (!download) return View(trip);
        var text = $"DashLane test payment receipt\nBooking: {id}\nReference: {trip.PaymentReference}\nMethod: {trip.PaymentMethod}\nLocked fare: {trip.LockedFare:0.00} CAD\nDiscount: {trip.Discount:0.00} CAD\nIncluded HST: {trip.IncludedTax:0.00} CAD\nPaid: {trip.Paid:0.00} CAD\n";
        return File(Encoding.UTF8.GetBytes(text), "text/plain; charset=utf-8", $"dashlane-receipt-{id}.txt");
    }

    [HttpGet]
    public async Task<IActionResult> DriverPhoto(int id, CancellationToken ct)
    {
        if (ReadDraft() is not { } draft) return NotFound();
        var driver = (await LiveDriversAsync(draft, ct)).SingleOrDefault(d => d.Id == id);
        var path = driver == null ? null : membership.MemberFilePath(driver.Member, driver.Member.PhotoFileName);
        var contentType = Path.GetExtension(path)?.ToLowerInvariant() switch { ".jpg" or ".jpeg" => "image/jpeg", ".png" => "image/png", ".webp" => "image/webp", _ => null };
        return path != null && contentType != null && System.IO.File.Exists(path) ? PhysicalFile(path, contentType) : NotFound();
    }

    private async Task<IActionResult> TripPost(Guid id, Func<int, Task<string?>> action, CancellationToken ct)
    {
        if (await MemberIdAsync() is not int memberId) return Challenge();
        if (await rides.TripAsync(id, memberId, false, ct) == null) return NotFound();
        if (!ModelState.IsValid)
        {
            TempData["RideNotice"] = "Check the submitted values and try again.";
            return RedirectToAction(nameof(Status), new { id });
        }
        var error = await action(memberId);
        TempData["RideNotice"] = error ?? "Your trip was updated.";
        return RedirectToAction(nameof(Status), new { id });
    }

    private CheckoutViewModel CheckoutModel(RideDraft draft, CheckoutInput? input = null)
    {
        var fare = CheckoutFare.From(draft);
        var notice = draft.ScheduledUtc > clock.GetUtcNow().UtcDateTime
            ? "Scheduled rides can be previewed. Test checkout supports immediate departures." : null;
        return new(draft, fare, notice == null, notice,
            input ?? new CheckoutInput(draft.Id, "Card", null, fare.Total));
    }

    private async Task<int?> MemberIdAsync() =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) && await membership.FindAsync(id) != null ? id : null;
    private async Task<IReadOnlyList<DriverProfile>> LiveDriversAsync(RideDraft draft, CancellationToken ct) =>
        await MemberIdAsync() is int id ? await rides.EligibleDriversAsync(draft.Pickup.Latitude, draft.Pickup.Longitude,
            draft.Quote.Tier, draft.Passengers, draft.RequiresWheelchairAccess, id, ct) : [];

    private async Task<RideConfirmationViewModel> ConfirmModelAsync(RideDraft draft, RideDriverSelectionInput? input, CancellationToken ct)
    {
        var matches = await LiveDriversAsync(draft, ct);
        var ratings = await rides.RatingsAsync(matches.Select(d => d.Id).ToArray(), ct);
        var options = matches.Select(d => new RideDriverOption(d.Id, d.Member.PublicName,
            ratings.GetValueOrDefault(d.Id),
            d.Member.PhotoFileName == null ? null : Url.Action(nameof(DriverPhoto), new { d.Id }), d.Vehicle.Make,
            d.Vehicle.Model, d.Vehicle.Year, d.Vehicle.Plate, d.Vehicle.PassengerCapacity, d.Vehicle.WheelchairAccessible,
            new RideDriverApproach(d.Id, new(d.Latitude, d.Longitude), Math.Max(2, (int)Math.Ceiling(
                RideService.DistanceKm(draft.Pickup.Latitude, draft.Pickup.Longitude, d.Latitude, d.Longitude) * 2.4))))).ToList();
        return new(draft, options, new RideDriverSelectionInput { DraftId = draft.Id,
            DriverId = options.FirstOrDefault(d => d.Id == input?.DriverId)?.Id ?? options.FirstOrDefault()?.Id,
            PickupInstructions = input?.PickupInstructions, DriverNote = input?.DriverNote });
    }

    private RideDraft? ReadDraft() => ReadDraft(TempData);
    internal static RideDraft? ReadDraft(ITempDataDictionary tempData)
    {
        try { return tempData.Peek(DraftKey) is string json ? JsonSerializer.Deserialize<RideDraft>(json) : null; }
        catch (JsonException) { tempData.Remove(DraftKey); return null; }
    }
}
