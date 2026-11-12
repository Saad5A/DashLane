using DashLane.Data;
using DashLane.Models;
using Microsoft.EntityFrameworkCore;

namespace DashLane.Services;

public partial class RideService(DashLaneContext db, TimeProvider clock)
{
    private const double DispatchRadiusKm = 10;
    private static readonly TimeSpan AcceptanceWindow = TimeSpan.FromSeconds(30);

    public Task<IReadOnlyList<TierFare>> QuoteAsync(RideQuoteRequest request, CancellationToken ct) =>
        CalculateFaresAsync(request, request.PromoCode, ct);

    private async Task<IReadOnlyList<TierFare>> CalculateFaresAsync(
        RideRouteEstimate request, string? promoCode, CancellationToken ct)
    {
        var latitude = request.PickupLatitude!.Value;
        var longitude = request.PickupLongitude!.Value;
        var drivers = await NearbyDriversAsync(latitude, longitude, ct);
        var pickups = await db.RideBookings.AsNoTracking().Where(ride => ride.Status == RideStatus.Requested)
            .Select(ride => new { ride.PickupLatitude, ride.PickupLongitude }).ToListAsync(ct);
        var demand = pickups.Count(pickup =>
            DistanceKm(latitude, longitude, pickup.PickupLatitude, pickup.PickupLongitude) <= DispatchRadiusKm);
        var ratio = drivers.Count == 0 ? (demand > 0 ? 2m : 0m) : (decimal)demand / drivers.Count;
        var surge = ratio <= 1.5m ? 1m : Math.Round(Math.Clamp(ratio, 1.25m, 2m), 2);

        var promotion = await FindPromotionAsync(promoCode, ct);
        return FareSchedule.Rates.Where(rate => request.Passengers <= rate.Capacity &&
                (!request.RequiresWheelchairAccess || rate.Tier == VehicleTier.WheelchairAccessible))
            .Select(rate =>
            {
                var subtotal = FareSchedule.EstimateTripFare(rate.Tier, request.DistanceKm, request.DurationMin, surge);
                var discount = subtotal >= 15m && promotion != null
                    ? FareSchedule.Money(Math.Clamp(promotion.DiscountAmount, 0m, subtotal)) : 0m;
                return new TierFare(rate.Tier, rate.BaseFee, rate.PerKm, rate.PerMin,
                    rate.Minimum, surge, subtotal, discount, subtotal - discount);
            }).ToList();
    }

    public async Task<DispatchResult?> DispatchAsync(int riderId, RideDispatchRequest request, CancellationToken ct)
    {
        // Microsoft.Data.Sqlite starts a non-deferred write transaction by default.
        // Take the writer lock before reading availability, including on timeout and acceptance.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var existing = await db.RideBookings.FindAsync([request.RequestId], ct);
        if (existing != null)
            return existing.RiderId == riderId ? Result(existing) : null;

        var fare = (await CalculateFaresAsync(request, null, ct)).SingleOrDefault(quote => quote.Tier == request.Tier);
        if (fare == null)
            return null;

        var ride = new RideBooking
        {
            Id = request.RequestId,
            RiderId = riderId,
            Tier = fare.Tier,
            Status = RideStatus.Requested,
            PickupLatitude = request.PickupLatitude!.Value,
            PickupLongitude = request.PickupLongitude!.Value,
            DestinationLatitude = request.DestinationLatitude!.Value,
            DestinationLongitude = request.DestinationLongitude!.Value,
            Passengers = request.Passengers,
            RequiresWheelchairAccess = request.RequiresWheelchairAccess,
            DistanceKm = request.DistanceKm,
            DurationMin = request.DurationMin,
            FareBeforeDiscount = fare.FareBeforeDiscount,
            SurgeMultiplier = fare.SurgeMultiplier,
            Fare = fare.Fare,
            RequestedUtc = clock.GetUtcNow().UtcDateTime
        };

        db.RideBookings.Add(ride);
        await OfferNextAsync(ride, ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Result(ride);
    }

    public Task<DispatchResult?> AcceptAsync(Guid id, int driverMemberId, CancellationToken ct) =>
        RespondAsync(id, driverMemberId, DriverRequestAction.Accept, ct);

    /// <summary>
    /// Accept/decline within the 30-second dispatch window, or advance
    /// Assigned → Arrived → InProgress → Completed after acceptance.
    /// </summary>
    public async Task<DispatchResult?> RespondAsync(Guid id, int driverMemberId, DriverRequestAction action,
        CancellationToken ct)
    {
        if (action is DriverRequestAction.Arrived or DriverRequestAction.Start or DriverRequestAction.Complete)
        {
            var expected = action switch
            {
                DriverRequestAction.Arrived => RideStatus.Assigned,
                DriverRequestAction.Start => RideStatus.Arrived,
                _ => RideStatus.InProgress
            };
            var error = await AdvanceTripAsync(id, driverMemberId, expected, demo: false, ct);
            if (error != null)
                return null;
            var advanced = await db.RideBookings.AsNoTracking().SingleOrDefaultAsync(booking => booking.Id == id, ct);
            return advanced == null ? null : Result(advanced);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var ride = await db.RideBookings.Include(booking => booking.Driver)
            .SingleOrDefaultAsync(booking => booking.Id == id, ct);
        if (ride?.Driver?.MemberId != driverMemberId)
            return null;

        var now = clock.GetUtcNow().UtcDateTime;
        switch (action)
        {
            case DriverRequestAction.Accept:
                if (ride.Status == RideStatus.Assigned)
                    break;
                if (ride.Status != RideStatus.Requested || ride.AcceptanceDeadlineUtc is not DateTime deadline ||
                    deadline <= now || ride.Driver.Availability != DriverAvailability.Dispatched)
                    return null;
                ride.Status = RideStatus.Assigned;
                ride.AssignedUtc = now;
                ride.PickupEtaUtc = now.AddMinutes(PickupMinutes(ride, ride.Driver));
                ride.AcceptanceDeadlineUtc = null;
                break;

            case DriverRequestAction.Decline:
                if (ride.Status != RideStatus.Requested || ride.AcceptanceDeadlineUtc is not DateTime declineDeadline ||
                    declineDeadline <= now || ride.Driver.Availability != DriverAvailability.Dispatched)
                    return null;
                ride.Driver.Availability = DriverAvailability.Available;
                ride.Driver = null;
                ride.DriverProfileId = null;
                ride.DriverNote = null;
                await OfferNextAsync(ride, ct);
                break;

            default:
                return null;
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Result(ride);
    }

    public async Task ExpireOffersAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var expiredIds = await db.RideBookings.AsNoTracking()
            .Where(ride => ride.Status == RideStatus.Requested && ride.AcceptanceDeadlineUtc <= now)
            .Select(ride => ride.Id).ToListAsync(ct);

        foreach (var id in expiredIds)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var ride = await db.RideBookings.Include(ride => ride.Driver)
                .SingleAsync(ride => ride.Id == id, ct);
            if (ride.Status == RideStatus.Requested && ride.AcceptanceDeadlineUtc <= now)
            {
                if (ride.Driver?.Availability == DriverAvailability.Dispatched)
                    ride.Driver.Availability = DriverAvailability.Available;

                ride.Driver = null;
                ride.DriverProfileId = null;
                ride.DriverNote = null;
                await OfferNextAsync(ride, ct);
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
            }
            db.ChangeTracker.Clear();
        }
    }

    public async Task<(Promotion? Promotion, CheckoutFare Fare, string? Error)> CheckoutPromoAsync(
        RideDraft draft, int riderId, string code, CancellationToken ct)
    {
        var fare = CheckoutFare.From(draft);
        if (fare.LockedTotal < 15m)
            return (null, fare, "Promotions require a minimum locked fare of $15.00.");

        var normalized = code.Trim().ToUpperInvariant();
        var now = clock.GetUtcNow().UtcDateTime;
        var promotion = await db.Promotions.SingleOrDefaultAsync(item => item.Code == normalized &&
            item.IsActive && item.StartsUtc <= now && item.EndsUtc >= now &&
            item.RedemptionCount < item.RedemptionLimit &&
            (item.EligibleRiderId == null || item.EligibleRiderId == riderId), ct);
        if (promotion == null || promotion.DiscountAmount <= 0)
            return (null, fare, "This promotion is invalid, expired, exhausted, or unavailable for your account.");

        return (promotion, CheckoutFare.From(draft,
            CheckoutFare.Money(Math.Min(fare.LockedTotal, promotion.DiscountAmount))), null);
    }

    public Task<RideBooking?> RiderBookingAsync(Guid id, int riderId, CancellationToken ct) =>
        db.RideBookings.AsNoTracking().SingleOrDefaultAsync(ride => ride.Id == id && ride.RiderId == riderId, ct);

    public async Task<CheckoutResult> CheckoutAsync(RideDraft draft, int riderId, CheckoutInput input, CancellationToken ct)
    {
        // SQLite takes its writer lock before reading the driver and promotion.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var existing = await db.RideBookings.FindAsync([draft.Id], ct);
        if (existing != null)
            return existing.RiderId == riderId ? new(existing.Id, null) : new(null, "This draft is unavailable.");

        var now = clock.GetUtcNow().UtcDateTime;
        if (draft.Confirmation?.DriverId is not > 0 || draft.ScheduledUtc > now)
            return new(null, "Checkout requires a live driver and an immediate departure.");
        if (input.Method is not ("Card" or "Wallet"))
            return new(null, "Choose a supported test payment method.");

        var fare = CheckoutFare.From(draft);
        Promotion? promotion = null;
        if (!string.IsNullOrWhiteSpace(input.PromoCode))
        {
            var validation = await CheckoutPromoAsync(draft, riderId, input.PromoCode, ct);
            if (validation.Error != null)
                return new(null, validation.Error);
            (promotion, fare) = (validation.Promotion, validation.Fare);
        }
        if (input.ExpectedTotal != fare.Total)
            return new(null, "The payment total changed. Review the fare and apply the promotion again.");

        var ride = new RideBooking
        {
            Id = draft.Id, RiderId = riderId, Tier = draft.Quote.Tier, Status = RideStatus.Requested,
            PickupLatitude = draft.Pickup.Latitude, PickupLongitude = draft.Pickup.Longitude,
            DestinationLatitude = draft.Destination.Latitude, DestinationLongitude = draft.Destination.Longitude,
            DistanceKm = draft.Route.DistanceKm, DurationMin = draft.Route.DurationMin,
            Passengers = draft.Passengers, RequiresWheelchairAccess = draft.RequiresWheelchairAccess,
            PickupInstructions = draft.Confirmation.PickupInstructions, DriverNote = draft.Confirmation.DriverNote,
            QuotedFare = fare.LockedTotal, FareBeforeDiscount = draft.Quote.FareBeforeDiscount,
            SurgeMultiplier = draft.Quote.SurgeMultiplier, PromoDiscount = fare.Discount,
            IncludedTax = fare.Tax, Fare = fare.Total, Promotion = promotion, RequestedUtc = now
        };
        db.RideBookings.Add(ride);
        await OfferNextAsync(ride, ct, draft.Confirmation.DriverId);
        if (ride.Driver == null)
            return new(null, "This driver is no longer available. Return to Driver & Details to choose another.");

        if (promotion != null)
            promotion.RedemptionCount++;
        db.PaymentTransactions.Add(new PaymentTransaction
        {
            RideBooking = ride, Amount = fare.Total, Discount = fare.Discount, Method = input.Method,
            Reference = $"sim_{ride.Id:N}", Status = "Completed", CreatedUtc = now
        });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(ride.Id, null);
    }

    private async Task OfferNextAsync(RideBooking ride, CancellationToken ct, int? preferredDriverId = null)
    {
        var attempted = await db.DispatchAttempts.Where(attempt => attempt.RideBookingId == ride.Id)
            .Select(attempt => attempt.DriverProfileId).ToListAsync(ct);
        var nearby = await EligibleDriversAsync(ride.PickupLatitude, ride.PickupLongitude,
            ride.Tier, ride.Passengers, ride.RequiresWheelchairAccess, ride.RiderId, ct);
        var driver = nearby.FirstOrDefault(driver => !attempted.Contains(driver.Id) &&
            (preferredDriverId == null || driver.Id == preferredDriverId));

        if (driver == null)
        {
            ride.Status = RideStatus.Cancelled;
            ride.CancellationReason = attempted.Count == 0 ? "NoEligibleDrivers" : "NoDriverAccepted";
            ride.CancelledUtc = clock.GetUtcNow().UtcDateTime;
            ride.CancellationFee = 0m;
            ride.AcceptanceDeadlineUtc = null;
            return;
        }

        var reserved = await db.DriverProfiles.FindAsync([driver.Id], ct);
        reserved!.Availability = DriverAvailability.Dispatched;
        ride.Driver = reserved;
        ride.AcceptanceDeadlineUtc = clock.GetUtcNow().UtcDateTime + AcceptanceWindow;
        ride.DispatchAttempts.Add(new DispatchAttempt { DriverProfileId = driver.Id });
    }

    public async Task<IReadOnlyList<DriverProfile>> EligibleDriversAsync(double latitude, double longitude,
        VehicleTier tier, int passengers, bool accessible, int riderId, CancellationToken ct)
    {
        var nearby = await NearbyDriversAsync(latitude, longitude, ct);
        return nearby.Where(driver => driver.MemberId != riderId && driver.Vehicle.Tier == tier &&
            driver.Vehicle.PassengerCapacity >= passengers &&
            (!(accessible || tier == VehicleTier.WheelchairAccessible) || driver.Vehicle.WheelchairAccessible))
            .ToList();
    }

    public async Task<Dictionary<int, DriverRating>> RatingsAsync(int[] ids, CancellationToken ct) =>
        await db.ReviewFeedback.AsNoTracking().Where(review => !review.IsDriver && review.RideBooking.Status == RideStatus.Completed &&
                review.RideBooking.DriverProfileId.HasValue && ids.Contains(review.RideBooking.DriverProfileId.Value) &&
                review.Rating >= 1 && review.Rating <= 5)
            .GroupBy(review => review.RideBooking.DriverProfileId!.Value)
            .Select(group => new { Id = group.Key, Rating = new DriverRating(group.Average(review => review.Rating), group.Count()) })
            .ToDictionaryAsync(item => item.Id, item => item.Rating, ct);

    private async Task<List<DriverProfile>> NearbyDriversAsync(double latitude, double longitude, CancellationToken ct)
    {
        var minLatitude = latitude - .1;
        var maxLatitude = latitude + .1;
        var drivers = await db.DriverProfiles.AsNoTracking().Include(driver => driver.Vehicle).Include(driver => driver.Member)
            .Where(driver => driver.Availability == DriverAvailability.Available &&
                driver.LicenseVerified && driver.BackgroundCheckVerified && driver.Member.PhoneVerified &&
                driver.Vehicle != null && driver.Latitude >= minLatitude && driver.Latitude <= maxLatitude)
            .ToListAsync(ct);

        return drivers.Select(driver => (Driver: driver,
                Distance: DistanceKm(latitude, longitude, driver.Latitude, driver.Longitude)))
            .Where(item => item.Distance <= DispatchRadiusKm).OrderBy(item => item.Distance)
            .ThenBy(item => item.Driver.Id).Select(item => item.Driver).ToList();
    }

    private Task<Promotion?> FindPromotionAsync(string? code, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code))
            return Task.FromResult<Promotion?>(null);

        var normalized = code.Trim().ToUpperInvariant();
        var now = clock.GetUtcNow().UtcDateTime;
        return db.Promotions.AsNoTracking().SingleOrDefaultAsync(promotion => promotion.Code == normalized &&
            promotion.IsActive && promotion.EligibleRiderId == null && promotion.StartsUtc <= now && promotion.EndsUtc >= now &&
            promotion.RedemptionCount < promotion.RedemptionLimit, ct);
    }

    private static DispatchResult Result(RideBooking ride) =>
        new(ride.Id, ride.Status, ride.AcceptanceDeadlineUtc, ride.Fare, ride.SurgeMultiplier, ride.CancellationReason);

    internal static double DistanceKm(double latitude, double longitude, double otherLatitude, double otherLongitude)
    {
        const double radians = Math.PI / 180;
        var latitudeTerm = Math.Sin((otherLatitude - latitude) * radians / 2);
        var longitudeTerm = Math.Sin((otherLongitude - longitude) * radians / 2);
        var haversine = latitudeTerm * latitudeTerm +
            Math.Cos(latitude * radians) * Math.Cos(otherLatitude * radians) * longitudeTerm * longitudeTerm;
        return 6371 * 2 * Math.Asin(Math.Sqrt(Math.Clamp(haversine, 0, 1)));
    }
}
