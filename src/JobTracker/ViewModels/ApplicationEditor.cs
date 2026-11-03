namespace JobTracker.ViewModels;

public sealed class ApplicationEditor : ObservableObject
{
    private string company, role, notes, error = "";
    private DateTime? appliedDate, interviewDate;
    private ApplicationStatus status;
    private readonly string original;
    public RelayCommand ClearInterviewCommand { get; }
    public int? Id { get; }
    public string Title => Id.HasValue ? "Edit application" : "A new opportunity";
    public string Subtitle => Id.HasValue ? "Keep the details and your next step up to date." : "Every next chapter starts with one application.";
    public string SaveLabel => Id.HasValue ? "Save changes" : "Add application";
    public ApplicationStatus[] Statuses { get; } = Enum.GetValues<ApplicationStatus>();
    public string Company { get => company; set { if (Set(ref company, value)) Error = ""; } }
    public string Role { get => role; set { if (Set(ref role, value)) Error = ""; } }
    public string Notes { get => notes; set { if (Set(ref notes, value)) Error = ""; } }
    public DateTime? AppliedDate { get => appliedDate; set { if (Set(ref appliedDate, value)) Error = ""; } }
    public DateTime? InterviewDate { get => interviewDate; set { if (Set(ref interviewDate, value)) Error = ""; } }
    public ApplicationStatus Status { get => status; set { if (Set(ref status, value)) Error = ""; } }
    public string Error { get => error; set { if (Set(ref error, value)) Raise(nameof(HasError)); } }
    public bool HasError => Error.Length > 0;
    public bool IsDirty => Snapshot() != original;

    public ApplicationEditor(JobApplication? item, DateOnly today)
    {
        ClearInterviewCommand = new(() => InterviewDate = null);
        Id = item?.Id;
        company = item?.Company ?? "";
        role = item?.Role ?? "";
        notes = item?.Notes ?? "";
        appliedDate = (item?.AppliedOn ?? today).ToDateTime(TimeOnly.MinValue);
        interviewDate = item?.InterviewOn?.ToDateTime(TimeOnly.MinValue);
        status = item?.Status ?? ApplicationStatus.Applied;
        original = Snapshot();
    }

    public bool Validate()
    {
        Error = string.IsNullOrWhiteSpace(Company) ? "Please enter a company name."
            : string.IsNullOrWhiteSpace(Role) ? "Please enter the job title."
            : AppliedDate is null || AppliedDate == DateTime.MinValue ? "Choose an application date."
            : InterviewDate?.Date < AppliedDate?.Date ? "Interview date must be on or after the application date."
            : !Enum.IsDefined(Status) ? "Choose a valid status." : "";
        return !HasError;
    }

    private string Snapshot() => System.Text.Json.JsonSerializer.Serialize(new { Company, Role, Notes, AppliedDate, InterviewDate, Status });
}
