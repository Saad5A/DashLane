namespace DashLane.Services;

/// <summary>Public contact details shown on the Help, Privacy, Security and Community Standards pages.</summary>
public class SiteOptions
{
    public const string SectionName = "Site";

    /// <summary>Where members send support, privacy, and safety requests. Leave empty until one is monitored.</summary>
    public string? SupportEmail { get; set; }

    public bool HasSupportEmail => !string.IsNullOrWhiteSpace(SupportEmail);
}
