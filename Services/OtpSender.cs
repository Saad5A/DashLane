using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DashLane.Models;

namespace DashLane.Services;

public enum OtpCheck
{
    Approved,
    Wrong,
    Expired,
    TooManyAttempts
}

/// <summary>Sends and checks SMS codes with Twilio Verify. Twilio generates, stores and expires the code.</summary>
public class OtpSender
{
    private const string VerifyBase = "https://verify.twilio.com/v2/Services";
    private static readonly SemaphoreSlim ServiceLock = new(1, 1);
    private static string? _resolvedServiceSid;

    private readonly OtpOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OtpSender> _logger;

    public OtpSender(OtpOptions options, IHttpClientFactory httpClientFactory, ILogger<OtpSender> logger)
    {
        _options = options;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public bool IsConfigured => _options.IsConfigured;

    public async Task<string> SendAsync(Member member, CancellationToken cancellationToken = default)
    {
        EnsureReady(member);
        var serviceSid = await ServiceSidAsync(cancellationToken);
        var form = new Dictionary<string, string>
        {
            ["To"] = member.PhoneNumber!,
            ["Channel"] = "sms",
            ["Locale"] = Text.IsFrench ? "fr" : "en"
        };

        var (ok, status, body) = await PostAsync($"{VerifyBase}/{serviceSid}/Verifications", form, cancellationToken);
        if (!ok)
        {
            var code = ErrorCode(body);
            _logger.LogWarning("Twilio Verify send failed: {Status} {Code} {Body}", status, code, body);
            throw new InvalidOperationException(code switch
            {
                21608 or 21614 => Text.T("Twilio's trial only texts numbers you've verified in the Twilio console. Add this number under Phone Numbers → Verified Caller IDs, or upgrade the account."),
                60200 or 21211 => Text.T("Enter a valid 10-digit Canadian mobile number."),
                60203 => Text.T("Too many codes were sent to this number. Wait 10 minutes and try again."),
                20003 => Text.T("The Twilio keys in .env were rejected. Check TWILIO_ACCOUNT_SID and TWILIO_AUTH_TOKEN."),
                _ => Text.T("We could not send the SMS verification code. Try again in a moment.")
            });
        }

        return Text.T("We sent a 6-digit code to {0}.", MaskPhone(member.PhoneNumber!));
    }

    public async Task<OtpCheck> CheckAsync(Member member, string code, CancellationToken cancellationToken = default)
    {
        EnsureReady(member);
        var serviceSid = await ServiceSidAsync(cancellationToken);
        var form = new Dictionary<string, string> { ["To"] = member.PhoneNumber!, ["Code"] = code.Trim() };
        var (ok, status, body) = await PostAsync($"{VerifyBase}/{serviceSid}/VerificationCheck", form, cancellationToken);

        if (ok)
        {
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.TryGetProperty("status", out var s) && s.GetString() == "approved"
                ? OtpCheck.Approved
                : OtpCheck.Wrong;
        }

        var error = ErrorCode(body);
        _logger.LogInformation("Twilio Verify check rejected: {Status} {Code}", status, error);
        return error switch
        {
            60202 => OtpCheck.TooManyAttempts,
            20404 => OtpCheck.Expired, // no pending verification: expired, already used, or never sent
            _ => throw new InvalidOperationException(Text.T("We couldn't check the code right now. Try again in a moment."))
        };
    }

    private void EnsureReady(Member member)
    {
        if (!_options.IsConfigured)
        {
            throw new InvalidOperationException(Text.T("SMS codes aren't set up yet. Add TWILIO_ACCOUNT_SID and TWILIO_AUTH_TOKEN to .env and restart the app."));
        }

        if (string.IsNullOrWhiteSpace(member.PhoneNumber))
        {
            throw new InvalidOperationException(Text.T("Enter a Canadian mobile number before sending a code."));
        }
    }

    private async Task<string> ServiceSidAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_options.VerifyServiceSid))
        {
            return _options.VerifyServiceSid!;
        }

        if (_resolvedServiceSid != null)
        {
            return _resolvedServiceSid;
        }

        await ServiceLock.WaitAsync(cancellationToken);
        try
        {
            if (_resolvedServiceSid != null)
            {
                return _resolvedServiceSid;
            }

            var client = Client();
            using (var list = await client.GetAsync(VerifyBase + "?PageSize=50", cancellationToken))
            {
                var json = await list.Content.ReadAsStringAsync(cancellationToken);
                if (list.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(json);
                    foreach (var service in doc.RootElement.GetProperty("services").EnumerateArray())
                    {
                        if (service.GetProperty("friendly_name").GetString() == "DashLane")
                        {
                            return _resolvedServiceSid = service.GetProperty("sid").GetString()!;
                        }
                    }
                }
            }

            var (ok, status, body) = await PostAsync(VerifyBase, new Dictionary<string, string> { ["FriendlyName"] = "DashLane" }, cancellationToken);
            if (!ok)
            {
                _logger.LogError("Could not create Twilio Verify service: {Status} {Body}", status, body);
                throw new InvalidOperationException(ErrorCode(body) == 20003
                    ? Text.T("The Twilio keys in .env were rejected. Check TWILIO_ACCOUNT_SID and TWILIO_AUTH_TOKEN.")
                    : Text.T("We could not send the SMS verification code. Try again in a moment."));
            }

            using var created = JsonDocument.Parse(body);
            _resolvedServiceSid = created.RootElement.GetProperty("sid").GetString()!;
            _logger.LogInformation("Created Twilio Verify service {Sid}. Set TWILIO_VERIFY_SERVICE_SID={Sid} in .env to reuse it.", _resolvedServiceSid, _resolvedServiceSid);
            return _resolvedServiceSid;
        }
        finally
        {
            ServiceLock.Release();
        }
    }

    private HttpClient Client()
    {
        var client = _httpClientFactory.CreateClient("twilio");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes(_options.AccountSid + ":" + _options.AuthToken)));
        return client;
    }

    private async Task<(bool Ok, int Status, string Body)> PostAsync(string url, Dictionary<string, string> form, CancellationToken cancellationToken)
    {
        using var response = await Client().PostAsync(url, new FormUrlEncodedContent(form), cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return (response.IsSuccessStatusCode, (int)response.StatusCode, body);
    }

    private static int? ErrorCode(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.TryGetProperty("code", out var code) && code.ValueKind == JsonValueKind.Number ? code.GetInt32() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string MaskPhone(string phone) =>
        phone.StartsWith("+1", StringComparison.Ordinal) && phone.Length == 12
            ? "+1 ••• ••• " + phone[^4..]
            : (phone.Length <= 4 ? phone : new string('•', phone.Length - 4) + phone[^4..]);
}
