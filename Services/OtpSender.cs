using System.Net.Http.Headers;
using System.Text;
using DashLane.Models;

namespace DashLane.Services;

public class OtpSender
{
    private readonly OtpOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OtpSender> _logger;

    public OtpSender(
        OtpOptions options,
        IHttpClientFactory httpClientFactory,
        ILogger<OtpSender> logger)
    {
        _options = options;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public string ChannelLabel => _options.HasTwilio ? "SMS ready" : "SMS not configured";

    public async Task<(bool Ok, string Notice)> SendAsync(Member member, string code, CancellationToken cancellationToken = default)
    {
        if (!_options.HasTwilio)
        {
            throw new InvalidOperationException(
                "SMS OTP is not configured. Add Otp__TwilioAccountSid, Otp__TwilioAuthToken, and Otp__TwilioFromNumber to your .env file, then restart the app.");
        }

        if (string.IsNullOrWhiteSpace(member.PhoneNumber))
        {
            throw new InvalidOperationException("Enter a Canadian mobile number before sending a code.");
        }

        await SendTwilioSmsAsync(member.PhoneNumber, code, cancellationToken);
        return (true, "We sent a 6-digit code by SMS to " + MaskPhone(member.PhoneNumber) + ".");
    }

    private async Task SendTwilioSmsAsync(string phoneNumber, string code, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("twilio");
        var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes(_options.TwilioAccountSid + ":" + _options.TwilioAuthToken));
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "https://api.twilio.com/2010-04-01/Accounts/" + _options.TwilioAccountSid + "/Messages.json");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["To"] = phoneNumber,
            ["From"] = _options.TwilioFromNumber!,
            ["Body"] = "Your DashLane verification code is " + code + ". It expires in 10 minutes."
        });

        using var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Twilio SMS failed: {Status} {Body}", response.StatusCode, body);
            throw new InvalidOperationException("The SMS provider could not send the code. Check your Twilio keys and From number in .env.");
        }
    }

    private static string MaskPhone(string phone)
    {
        if (phone.StartsWith("+1", StringComparison.Ordinal) && phone.Length == 12)
        {
            return "+1••••••" + phone[^4..];
        }

        return phone.Length <= 4 ? phone : new string('•', phone.Length - 4) + phone[^4..];
    }
}
