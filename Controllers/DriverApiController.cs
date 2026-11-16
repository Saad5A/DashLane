using System.Security.Claims;
using DashLane.Models;
using DashLane.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DashLane.Controllers;

[ApiController]
[Authorize]
[Route("api/driver")]
public class DriverApiController(DriverService drivers, RideService rides, MembershipService membership) : ControllerBase
{
    [HttpGet("verification")]
    public async Task<ActionResult<DriverVerificationStatus>> Verification(CancellationToken ct)
    {
        if (await CurrentMemberIdAsync() is not int memberId)
            return Unauthorized();
        return Ok(await drivers.VerificationStatusAsync(memberId, ct));
    }

    [HttpPost("estimate-payout")]
    public async Task<ActionResult<EstimatePayoutResult>> EstimatePayout(EstimatePayoutRequest request, CancellationToken ct)
    {
        if (await CurrentMemberIdAsync() is null)
            return Unauthorized();
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        try
        {
            return Ok(drivers.EstimatePayout(request));
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("toggle-availability")]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult<ToggleAvailabilityResult>> ToggleAvailability(ToggleAvailabilityRequest request,
        CancellationToken ct)
    {
        if (await CurrentMemberIdAsync() is not int memberId)
            return Unauthorized();

        var result = await drivers.ToggleAvailabilityAsync(memberId, request.Online, ct);
        return result == null
            ? BadRequest(new { message = "Complete driver onboarding before going online." })
            : Ok(result);
    }

    [HttpPost("requests/{bookingId:guid}/respond")]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult<DispatchResult>> Respond(Guid bookingId, DriverRequestRespondInput input,
        CancellationToken ct)
    {
        if (await CurrentMemberIdAsync() is not int memberId)
            return Unauthorized();
        if (!ModelState.IsValid || input.Action is not DriverRequestAction action)
            return ValidationProblem(ModelState);

        var result = await rides.RespondAsync(bookingId, memberId, action, ct);
        return result == null
            ? Conflict(new { message = "This ride request is unavailable or the action is no longer valid." })
            : Ok(result);
    }

    [HttpPost("simulate-request")]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult<IncomingRiderRequestView>> SimulateRequest(SimulateRequestInput input,
        CancellationToken ct)
    {
        if (await CurrentMemberIdAsync() is not int memberId)
            return Unauthorized();
        if (input.PostedRideId == Guid.Empty)
            return BadRequest(new { message = "A posted ride id is required." });

        var (request, error) = await drivers.SimulateIncomingRequestAsync(memberId, input.PostedRideId, ct);
        return error != null || request == null
            ? BadRequest(new { message = error ?? "Unable to simulate a rider request." })
            : Ok(request);
    }

    [HttpGet("pending-request")]
    public async Task<ActionResult<IncomingRiderRequestView?>> PendingRequest(CancellationToken ct)
    {
        if (await CurrentMemberIdAsync() is not int memberId)
            return Unauthorized();
        var driver = await drivers.FindByMemberAsync(memberId, ct);
        if (driver == null)
            return Ok(null);
        return Ok(await drivers.FindPendingRequestAsync(driver.Id, ct));
    }

    [HttpPost("rides")]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult<PostedRideResult>> PublishRide(PublishPostedRideRequest request, CancellationToken ct)
    {
        if (await CurrentMemberIdAsync() is not int memberId)
            return Unauthorized();
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var (result, error) = await drivers.PublishRideAsync(memberId, request, ct);
        if (result?.RequiresLicenseVerification == true)
            return StatusCode(StatusCodes.Status403Forbidden, result);
        if (error != null || result == null)
            return BadRequest(new { message = error ?? "Unable to publish this ride." });
        return Ok(result);
    }

    private async Task<int?> CurrentMemberIdAsync()
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var memberId))
            return null;
        return await membership.FindAsync(memberId) == null ? null : memberId;
    }
}
