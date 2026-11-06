namespace DashLane.Services;

public class OtpOptions
{
    public const string SectionName = "Otp";

    public string? TwilioAccountSid { get; set; }
    public string? TwilioAuthToken { get; set; }
    public string? TwilioFromNumber { get; set; }

    public bool HasTwilio =>
        !string.IsNullOrWhiteSpace(TwilioAccountSid) &&
        !string.IsNullOrWhiteSpace(TwilioAuthToken) &&
        !string.IsNullOrWhiteSpace(TwilioFromNumber);
}
