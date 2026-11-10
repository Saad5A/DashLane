namespace DashLane.Models;

public class HomeIndexViewModel
{
    public int MemberCount { get; set; }
    public int PhoneVerifiedCount { get; set; }
    public int DocumentsSubmittedCount { get; set; }
    public int CompletedCount { get; set; }

    /// <summary>Set for signed-in members: where "Continue setup" should go.</summary>
    public string? ContinueAction { get; set; }
}
