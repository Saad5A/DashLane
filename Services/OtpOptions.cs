namespace DashLane.Services;

/// <summary>
/// SMS one-time codes through Twilio Verify. Verify sends Twilio's own pre-approved message,
/// so it works on trial accounts (to numbers verified in the Twilio console), unlike the Messages API.
/// Read from TWILIO_ACCOUNT_SID, TWILIO_AUTH_TOKEN and TWILIO_VERIFY_SERVICE_SID.
/// </summary>
public class OtpOptions
{
    public string? AccountSid { get; set; }
    public string? AuthToken { get; set; }

    /// <summary>Optional. When empty, a Verify service named "DashLane" is found or created on first use.</summary>
    public string? VerifyServiceSid { get; set; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(AccountSid) && !string.IsNullOrWhiteSpace(AuthToken);

    public static OtpOptions FromConfiguration(IConfiguration configuration) => new()
    {
        AccountSid = configuration["TWILIO_ACCOUNT_SID"],
        AuthToken = configuration["TWILIO_AUTH_TOKEN"],
        VerifyServiceSid = configuration["TWILIO_VERIFY_SERVICE_SID"],
    };
}
