using System.Security.Claims;
using DashLane.Models;
using DashLane.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace DashLane.Controllers;

[ApiController]
[Authorize]
[Route("api/rides")]
public class RidesApiController(RideService rides, MembershipService membership, ITempDataDictionaryFactory tempData,
    IWebHostEnvironment environment) : ControllerBase
{
    [HttpGet("{id:guid}/status")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Status(Guid id, CancellationToken ct)
    {
        if (!TryMemberId(out var memberId)) return Unauthorized();
        var trip = await rides.TripAsync(id, memberId, environment.IsDevelopment(), ct);
        return trip == null ? NotFound() : Ok(trip);
    }

    [HttpPost("{id:guid}/cancel"), ValidateAntiForgeryToken]
    public Task<IActionResult> Cancel(Guid id, TripCancelInput input, CancellationToken ct) =>
        TripAction(memberId => rides.CancelTripAsync(id, memberId, input.ExpectedFee, ct));

    [HttpPost("{id:guid}/advance"), ValidateAntiForgeryToken]
    public Task<IActionResult> Advance(Guid id, TripAdvanceInput input, CancellationToken ct) =>
        TripAction(memberId => rides.AdvanceTripAsync(id, memberId, input.ExpectedStatus, environment.IsDevelopment(), ct));

    [HttpPost("{id:guid}/chat"), ValidateAntiForgeryToken]
    public Task<IActionResult> Chat(Guid id, TripTextInput input, CancellationToken ct) =>
        TripAction(memberId => rides.ChatAsync(id, memberId, input.Text, ct));

    [HttpPost("{id:guid}/location"), ValidateAntiForgeryToken]
    public Task<IActionResult> LocationUpdate(Guid id, TripLocationInput input, CancellationToken ct) =>
        TripAction(memberId => rides.UpdateTripLocationAsync(id, memberId, input, ct));

    [HttpPost("{id:guid}/incident"), ValidateAntiForgeryToken]
    public Task<IActionResult> Incident(Guid id, TripTextInput input, CancellationToken ct) =>
        TripAction(memberId => rides.IncidentAsync(id, memberId, input.Text, false, ct));

    [HttpPost("{id:guid}/sos"), ValidateAntiForgeryToken]
    public Task<IActionResult> Sos(Guid id, CancellationToken ct) =>
        TripAction(memberId => rides.IncidentAsync(id, memberId, "Simulated emergency contact notification", true, ct));

    [HttpPost("{id:guid}/review"), ValidateAntiForgeryToken]
    public Task<IActionResult> Review(Guid id, TripReviewInput input, CancellationToken ct) =>
        TripAction(memberId => rides.ReviewTripAsync(id, memberId, input, ct));

    private bool TryMemberId(out int id) => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out id);

    private async Task<IActionResult> TripAction(Func<int, Task<string?>> action)
    {
        if (!TryMemberId(out var id)) return Unauthorized();
        var error = await action(id);
        if (error == "Trip unavailable.") return NotFound();
        return error == null ? NoContent() : Conflict(new { message = error });
    }
    [HttpPost("validate-promo")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ValidatePromo(PromoRequest input, CancellationToken ct)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var riderId) ||
            await membership.FindAsync(riderId) == null)
            return Unauthorized();
        if (RidesController.ReadDraft(tempData.GetTempData(HttpContext)) is not { Confirmation: not null } draft ||
            draft.Id != input.DraftId || await rides.RiderBookingAsync(input.DraftId, riderId, ct) != null)
            return Conflict(new { message = "This ride draft is unavailable." });

        var result = await rides.CheckoutPromoAsync(draft, riderId, input.Code, ct);
        return result.Error == null
            ? Ok(new { code = result.Promotion!.Code, result.Fare })
            : BadRequest(new { message = result.Error });
    }
    [HttpPost("quote")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<TierFare>>> Quote(RideQuoteRequest request, CancellationToken ct)
    {
        var signedIn = User.Identity?.IsAuthenticated == true;
        if (request.PickupId != null || request.DestinationId != null)
        {
            var route = RideRouteCatalog.Find(request.PickupId, request.DestinationId);
            if (route == null)
                return BadRequest("Choose two different supported stops.");

            request = RideRouteCatalog.QuoteRequest(route, request.Passengers, request.RequiresWheelchairAccess,
                signedIn ? request.PromoCode : null);
        }
        else if (!signedIn)
        {
            return Unauthorized();
        }

        return Ok(await rides.QuoteAsync(request, ct));
    }

    [HttpPost("dispatch")]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult<DispatchResult>> Dispatch(RideDispatchRequest request, CancellationToken ct)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var memberId) ||
            await membership.FindAsync(memberId) == null)
            return Unauthorized();

        if (request.RequestId == Guid.Empty)
            return BadRequest("A non-empty requestId is required.");

        var result = await rides.DispatchAsync(memberId, request, ct);
        return result == null ? Conflict("The ride request ID or selected vehicle tier is unavailable.") : Ok(result);
    }

    [HttpPost("{id:guid}/accept")]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult<DispatchResult>> Accept(Guid id, CancellationToken ct)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var memberId))
            return Unauthorized();

        var result = await rides.AcceptAsync(id, memberId, ct);
        return result == null ? Conflict("This ride offer is unavailable.") : Ok(result);
    }
}
