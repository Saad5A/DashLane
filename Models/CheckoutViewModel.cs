using System.ComponentModel.DataAnnotations;

namespace DashLane.Models;

public record CheckoutFare(decimal Base, decimal Distance, decimal Duration, decimal Surge,
    decimal MinimumAdjustment, decimal LockedTotal, decimal Tax, decimal Discount, decimal Total)
{
    public static CheckoutFare From(RideDraft draft, decimal discount = 0)
        => From(draft.Quote, draft.Route.DistanceKm, draft.Route.DurationMin, discount);

    public static CheckoutFare From(TierFare quote, decimal distanceKm, decimal durationMin, decimal discount = 0)
    {
        var distance = distanceKm * quote.RatePerKm;
        var duration = durationMin * quote.RatePerMin;
        var subtotal = quote.BaseFee + distance + duration;
        var surge = Money(subtotal * quote.SurgeMultiplier) - quote.BaseFee - Money(distance) - Money(duration);
        var total = quote.Fare - discount;
        // The confirmed quote includes Ontario HST. A merchant discount lowers the inclusive total.
        return new(quote.BaseFee, Money(distance), Money(duration), surge,
            quote.Fare - Money(subtotal * quote.SurgeMultiplier), quote.Fare,
            Money(total * 13m / 113m), discount, total);
    }

    internal static decimal Money(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);
}

public record CheckoutViewModel(RideDraft Draft, CheckoutFare Fare, bool CanPay, string? Notice = null,
    CheckoutInput? Input = null);
public record PromoRequest(Guid DraftId, [Required, StringLength(32)] string Code);
public record CheckoutInput(Guid DraftId, [Required] string Method, [StringLength(32)] string? PromoCode,
    decimal ExpectedTotal);
public record CheckoutResult(Guid? BookingId, string? Error);
