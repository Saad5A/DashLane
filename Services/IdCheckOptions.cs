namespace DashLane.Services;

/// <summary>
/// Automated ID document checks through ID Analyzer (free plan: 100 API credits, no card required).
/// One basic scan costs 1 credit.
/// </summary>
public class IdCheckOptions
{
    public const string SectionName = "IdCheck";

    public string? IdAnalyzerApiKey { get; set; }

    /// <summary>A KYC profile id from the ID Analyzer portal, or a preset: security_none, security_low, security_medium, security_high.</summary>
    public string Profile { get; set; } = "security_medium";

    /// <summary>https://api2.idanalyzer.com (US) or https://api2-eu.idanalyzer.com (EU).</summary>
    public string BaseUrl { get; set; } = "https://api2.idanalyzer.com";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(IdAnalyzerApiKey);
}
