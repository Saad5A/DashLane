using System.Net.Http.Json;
using System.Text.Json;
using DashLane.Models;

namespace DashLane.Services;

public record IdCheckResult(string? Decision, string Summary, string? TransactionId)
{
    public bool Ran => Decision != null;
}

/// <summary>Calls ID Analyzer's /scan endpoint to OCR the document and cross-check it against the member's details.</summary>
public class IdVerifier
{
    private static readonly string[] ImageExtensions = { ".jpg", ".jpeg", ".png", ".webp" };

    private readonly IdCheckOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<IdVerifier> _logger;

    public IdVerifier(IdCheckOptions options, IHttpClientFactory httpClientFactory, ILogger<IdVerifier> logger)
    {
        _options = options;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public bool IsConfigured => _options.IsConfigured;

    public async Task<IdCheckResult> CheckAsync(Member member, string filePath, string documentType, CancellationToken cancellationToken = default)
    {
        if (!_options.IsConfigured)
        {
            return new IdCheckResult(null, ("Automatic check isn't set up. Your document will be reviewed manually."), null);
        }

        if (!ImageExtensions.Contains(Path.GetExtension(filePath).ToLowerInvariant()))
        {
            return new IdCheckResult(null, ("Automatic checks only run on photos. PDFs are reviewed manually."), null);
        }

        var payload = new Dictionary<string, object?>
        {
            ["profile"] = _options.Profile,
            ["document"] = Convert.ToBase64String(await File.ReadAllBytesAsync(filePath, cancellationToken)),
            ["restrictCountry"] = "CA",
            ["restrictType"] = documentType switch
            {
                "Passport" => "P",
                "Driver's Licence" => "D",
                _ => "I"
            }
        };
        if (!string.IsNullOrWhiteSpace(member.LegalName))
        {
            payload["verifyName"] = member.LegalName;
        }

        if (member.DateOfBirth is { } dob)
        {
            payload["verifyDob"] = dob.ToString("yyyy-MM-dd");
        }

        try
        {
            var client = _httpClientFactory.CreateClient("idanalyzer");
            using var request = new HttpRequestMessage(HttpMethod.Post, _options.BaseUrl.TrimEnd('/') + "/scan")
            {
                Content = JsonContent.Create(payload)
            };
            request.Headers.Add("X-API-KEY", _options.IdAnalyzerApiKey);
            request.Headers.Accept.ParseAdd("application/json");

            using var response = await client.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            if (!response.IsSuccessStatusCode || !root.TryGetProperty("decision", out var decisionElement))
            {
                var message = root.TryGetProperty("error", out var error) && error.TryGetProperty("message", out var m)
                    ? m.GetString()
                    : response.StatusCode.ToString();
                _logger.LogWarning("ID Analyzer scan failed: {Status} {Message}", (int)response.StatusCode, message);
                return new IdCheckResult(null, ("The automatic check couldn't run. Your document will be reviewed manually."), null);
            }

            var decision = decisionElement.GetString()?.ToLowerInvariant();
            var transactionId = root.TryGetProperty("transactionId", out var tx) ? tx.GetString() : null;
            var reasons = new List<string>();
            if (root.TryGetProperty("warning", out var warnings) && warnings.ValueKind == JsonValueKind.Array)
            {
                foreach (var warning in warnings.EnumerateArray())
                {
                    var warningDecision = warning.TryGetProperty("decision", out var wd) ? wd.GetString() : null;
                    if (warningDecision is "reject" or "review" &&
                        warning.TryGetProperty("description", out var description) &&
                        description.GetString() is { Length: > 0 } text &&
                        !reasons.Contains(text))
                    {
                        reasons.Add(text);
                    }
                }
            }

            var summary = decision switch
            {
                "accept" => ("Document passed the automatic check."),
                "review" => ("Document needs a manual review."),
                _ => ("Document didn't pass the automatic check.")
            };
            if (reasons.Count > 0)
            {
                summary += " " + string.Join(" · ", reasons.Take(3));
            }

            return new IdCheckResult(decision, summary, transactionId);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogWarning(exception, "ID Analyzer scan errored");
            return new IdCheckResult(null, ("The automatic check couldn't run. Your document will be reviewed manually."), null);
        }
    }
}
