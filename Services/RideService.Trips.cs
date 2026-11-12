using DashLane.Models;
using Microsoft.EntityFrameworkCore;

namespace DashLane.Services;

public partial class RideService
{
    private IQueryable<RideBooking> ParticipantTrips(int memberId) => db.RideBookings
        .Where(ride => ride.RiderId == memberId ||
            (ride.AssignedUtc != null && ride.Driver != null && ride.Driver.MemberId == memberId))
        .Include(ride => ride.Driver).ThenInclude(driver => driver!.Member)
        .Include(ride => ride.Driver).ThenInclude(driver => driver!.Vehicle);

    internal static int PickupMinutes(RideBooking ride, DriverProfile driver) =>
        Math.Max(2, (int)Math.Ceiling(DistanceKm(ride.PickupLatitude, ride.PickupLongitude,
            driver.Latitude, driver.Longitude) * 2.4));

    private static bool IsLate(RideBooking ride, DateTime now) =>
        ride.PickupEtaUtc is DateTime eta && (ride.ArrivedUtc ?? now) > eta.AddMinutes(5);

    private static bool CanCancel(RideBooking ride) =>
        ride.Status is RideStatus.Requested or RideStatus.Assigned or RideStatus.Arrived;

    private static bool CanChat(RideBooking ride) =>
        ride.Status is RideStatus.Assigned or RideStatus.Arrived or RideStatus.InProgress;

    private static decimal CancellationCharge(RideBooking ride, DateTime now) =>
        ride.AssignedUtc is DateTime assigned && now > assigned.AddMinutes(2) && !IsLate(ride, now) ? 5m : 0m;

    private static RideMapPoint Position(RideBooking ride) => CanChat(ride) && ride.Driver is { } driver
        ? new(driver.Latitude, driver.Longitude) : new(ride.PickupLatitude, ride.PickupLongitude);

    public async Task<TripStatusViewModel?> TripAsync(Guid id, int memberId, bool demo, CancellationToken ct)
    {
        var ride = await ParticipantTrips(memberId).AsNoTracking().SingleOrDefaultAsync(ride => ride.Id == id, ct);
        if (ride == null) return null;

        var driver = ride.AssignedUtc == null ? null : ride.Driver;
        var isDriver = ride.RiderId != memberId;
        var now = clock.GetUtcNow().UtcDateTime;
        var payment = isDriver ? null : await db.PaymentTransactions.AsNoTracking()
            .FirstOrDefaultAsync(payment => payment.RideBookingId == id && payment.Status == "Completed", ct);
        var reviewed = await db.ReviewFeedback.AnyAsync(review => review.RideBookingId == id && review.IsDriver == isDriver, ct);
        var messages = await db.TripMessages.AsNoTracking().Where(message => message.RideBookingId == id)
            .OrderByDescending(message => message.Id).Take(100).ToListAsync(ct);
        var chat = messages.AsEnumerable().Reverse()
            .Select(message => new TripChatLine(message.IsDriver ? "Driver" : "Rider", message.Text, message.CreatedUtc)).ToList();
        if (driver != null && !string.IsNullOrWhiteSpace(ride.DriverNote))
            chat.Insert(0, new TripChatLine("Rider", ride.DriverNote, ride.RequestedUtc));

        return new TripStatusViewModel(id, ride.Status.ToString(), isDriver, driver?.Member.PublicName,
            driver == null ? null : $"{driver.Vehicle.Make} {driver.Vehicle.Model}", driver?.Vehicle.Plate,
            new(ride.PickupLatitude, ride.PickupLongitude), new(ride.DestinationLatitude, ride.DestinationLongitude),
            driver != null && CanChat(ride) ? Position(ride) : null, ride.PickupInstructions, ride.AcceptanceDeadlineUtc,
            ride.PickupEtaUtc, now, ride.Status == RideStatus.Assigned && IsLate(ride, now),
            !isDriver && CanCancel(ride) ? CancellationCharge(ride, now) : null,
            ride.CancellationFee, ride.CancellationReason, ride.QuotedFare ?? ride.FareBeforeDiscount,
            ride.SurgeMultiplier, ride.PromoDiscount, ride.IncludedTax, payment?.Amount, payment?.Method,
            payment?.Reference, reviewed, chat, (isDriver || demo) &&
                (ride.Status is RideStatus.Assigned or RideStatus.Arrived or RideStatus.InProgress ||
                 demo && ride.Status == RideStatus.Requested && ride.DriverProfileId != null));
    }

    public Task<string?> CancelTripAsync(Guid id, int memberId, decimal expectedFee, CancellationToken ct) =>
        ChangeTripAsync(id, memberId, ride =>
        {
            if (ride.RiderId != memberId) return "Only the rider can cancel this trip.";
            if (ride.Status == RideStatus.Cancelled) return null;
            if (!CanCancel(ride)) return "This trip can no longer be cancelled.";
            var now = clock.GetUtcNow().UtcDateTime;
            var fee = CancellationCharge(ride, now);
            if (fee != expectedFee) return "The cancellation fee changed. Review the updated fee and confirm again.";
            ReleaseDriver(ride);
            ride.Status = RideStatus.Cancelled;
            ride.CancelledUtc = now;
            ride.CancellationFee = fee;
            ride.CancellationReason = "RiderCancelled";
            ride.AcceptanceDeadlineUtc = null;
            return null;
        }, ct);

    public Task<string?> AdvanceTripAsync(Guid id, int memberId, RideStatus expected, bool demo, CancellationToken ct) =>
        ChangeTripAsync(id, memberId, ride =>
        {
            if (ride.Driver == null || (!demo && ride.Driver.MemberId != memberId))
                return "Only the assigned driver can advance this trip.";
            if (ride.Status != expected) return "Trip status changed. Refresh and try again.";

            var now = clock.GetUtcNow().UtcDateTime;
            switch (ride.Status)
            {
                case RideStatus.Requested when demo && ride.AcceptanceDeadlineUtc > now &&
                    ride.Driver.Availability == DriverAvailability.Dispatched:
                    ride.Status = RideStatus.Assigned;
                    ride.AssignedUtc = now;
                    ride.PickupEtaUtc = now.AddMinutes(PickupMinutes(ride, ride.Driver));
                    ride.AcceptanceDeadlineUtc = null;
                    break;
                case RideStatus.Assigned:
                    ride.Status = RideStatus.Arrived;
                    ride.ArrivedUtc = now;
                    break;
                case RideStatus.Arrived:
                    ride.Status = RideStatus.InProgress;
                    ride.Driver.Availability = DriverAvailability.OnTrip;
                    break;
                case RideStatus.InProgress:
                    ReleaseDriver(ride);
                    ride.Status = RideStatus.Completed;
                    break;
                default:
                    return "This trip cannot advance.";
            }
            return null;
        }, ct);

    public Task<string?> ChatAsync(Guid id, int memberId, string text, CancellationToken ct) =>
        ChangeTripAsync(id, memberId, ride =>
        {
            if (!CanChat(ride)) return "Chat is available from assignment until the trip ends.";
            db.TripMessages.Add(new TripMessage { RideBookingId = id, IsDriver = ride.RiderId != memberId,
                Text = text.Trim(), CreatedUtc = clock.GetUtcNow().UtcDateTime });
            return null;
        }, ct);

    public Task<string?> UpdateTripLocationAsync(Guid id, int memberId, TripLocationInput input, CancellationToken ct) =>
        ChangeTripAsync(id, memberId, ride =>
        {
            if (!CanChat(ride) || ride.Driver?.MemberId != memberId)
                return "Only the driver can share a location during an active trip.";
            ride.Driver.Latitude = input.Latitude!.Value;
            ride.Driver.Longitude = input.Longitude!.Value;
            return null;
        }, ct);

    public Task<string?> IncidentAsync(Guid id, int memberId, string text, bool sos, CancellationToken ct) =>
        ChangeTripAsync(id, memberId, ride =>
        {
            if (sos && !CanChat(ride)) return "The SOS simulation is available during an active assigned ride.";
            var point = Position(ride);
            db.TripIncidents.Add(new TripIncident { RideBookingId = id, ReporterId = memberId,
                IsSos = sos, Text = text.Trim(), Latitude = point.Latitude, Longitude = point.Longitude,
                CreatedUtc = clock.GetUtcNow().UtcDateTime });
            return null;
        }, ct);

    public async Task<string?> ReviewTripAsync(Guid id, int memberId, TripReviewInput input, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var ride = await ParticipantTrips(memberId).SingleOrDefaultAsync(ride => ride.Id == id, ct);
        if (ride == null) return "Trip unavailable.";
        if (ride.Status != RideStatus.Completed) return "Reviews open after the trip completes.";

        var isDriver = ride.RiderId != memberId;
        if (await db.ReviewFeedback.AnyAsync(review => review.RideBookingId == id && review.IsDriver == isDriver, ct))
            return "You already reviewed this trip.";

        db.ReviewFeedback.Add(new ReviewFeedback { RideBookingId = id, IsDriver = isDriver, Rating = input.Rating,
            Comment = input.Comment?.Trim(), CreatedUtc = clock.GetUtcNow().UtcDateTime });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return null;
    }

    private async Task<string?> ChangeTripAsync(Guid id, int memberId, Func<RideBooking, string?> change, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var ride = await ParticipantTrips(memberId).SingleOrDefaultAsync(ride => ride.Id == id, ct);
        if (ride == null) return "Trip unavailable.";
        var error = change(ride);
        if (error != null) return error;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return null;
    }

    private static void ReleaseDriver(RideBooking ride)
    {
        if (ride.Driver?.Availability is DriverAvailability.Dispatched or DriverAvailability.OnTrip)
            ride.Driver.Availability = DriverAvailability.Available;
    }
}
