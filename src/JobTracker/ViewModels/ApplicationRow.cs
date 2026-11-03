using System.Globalization;

namespace JobTracker.ViewModels;

public sealed class ApplicationRow(JobApplication model, DateOnly today)
{
    public JobApplication Model { get; } = model;
    public int Id => Model.Id;
    public string Company => Model.Company;
    public string Role => Model.Role;
    public string Initial => Company[..1].ToUpperInvariant();
    public string Status => Model.Status.ToString();
    public string AppliedDate => Model.AppliedOn.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);
    public string InterviewDate => Model.InterviewOn?.ToString("ddd, MMM d", CultureInfo.InvariantCulture) ?? "Not scheduled";
    public string Notes => string.IsNullOrWhiteSpace(Model.Notes) ? "No notes yet. Add a few thoughts, a contact, or your next step." : Model.Notes;
    public bool IsUpcoming => Model.InterviewOn >= today && Model.Status is ApplicationStatus.Applied or ApplicationStatus.Interviewing;
    public string InterviewHint => !IsUpcoming ? "Interview date" : Model.InterviewOn == today ? "INTERVIEW TODAY" : "UPCOMING INTERVIEW";
    public string BadgeBackground => Model.Status switch
    {
        ApplicationStatus.Interviewing => "#EDE9FE", ApplicationStatus.Offer => "#DDF4E9",
        ApplicationStatus.Rejected => "#FDE9E8", ApplicationStatus.Withdrawn => "#ECEFF3", _ => "#E5EEFF"
    };
    public string BadgeForeground => Model.Status switch
    {
        ApplicationStatus.Interviewing => "#6A46B4", ApplicationStatus.Offer => "#147D54",
        ApplicationStatus.Rejected => "#BA4A44", ApplicationStatus.Withdrawn => "#667085", _ => "#3B65B6"
    };
    public string AvatarBackground => (Id % 4) switch { 0 => "#E9E5FA", 1 => "#E0F1ED", 2 => "#E8EDF9", _ => "#FBEBD9" };
    public string AvatarForeground => (Id % 4) switch { 0 => "#7256A8", 1 => "#258376", 2 => "#526EA8", _ => "#AC783D" };
}
