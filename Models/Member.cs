namespace DashLane.Models;

public class Member
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
    public string? PasswordHash { get; set; }
    public string? GoogleSubjectId { get; set; }
    public string FullName { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }

    public string? LegalName { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? HomeCity { get; set; }
    public string? PreferredLanguage { get; set; }

    public string? PhotoFileName { get; set; }
    public string? Bio { get; set; }
    public string Languages { get; set; } = "";
    public string Preferences { get; set; } = "";
    public DateTime? ProfileCompletedUtc { get; set; }

    public string? PhoneNumber { get; set; }
    public bool PhoneVerified { get; set; }
    public string? PhoneCodeHash { get; set; }
    public DateTime? PhoneCodeExpiresUtc { get; set; }

    public string? DocumentType { get; set; }
    public string? IssuingCountry { get; set; }
    public string? DocumentFileName { get; set; }
    public DateTime? DocumentSubmittedUtc { get; set; }

    /// <summary>Automated document check result: accept, review, reject, or null when no check ran.</summary>
    public string? DocumentCheckDecision { get; set; }
    public string? DocumentCheckSummary { get; set; }
    public string? DocumentCheckId { get; set; }
    public DateTime? DocumentCheckedUtc { get; set; }

    public DateTime? OnboardingCompletedUtc { get; set; }

    public IReadOnlyList<string> LanguageList =>
        Languages.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public IReadOnlyList<string> PreferenceList =>
        Preferences.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public string PublicName
    {
        get
        {
            var source = string.IsNullOrWhiteSpace(LegalName) ? FullName : LegalName;
            var parts = source.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return "New member";
            }

            if (parts.Length == 1)
            {
                return parts[0];
            }

            return parts[0] + " " + char.ToUpperInvariant(parts[^1][0]) + ".";
        }
    }

    public int CompletedSteps
    {
        get
        {
            var count = 0;
            if (DateOfBirth.HasValue && !string.IsNullOrWhiteSpace(LegalName)) count++;
            if (ProfileCompletedUtc.HasValue) count++;
            if (PhoneVerified) count++;
            if (!string.IsNullOrWhiteSpace(DocumentFileName)) count++;
            return count;
        }
    }
}
