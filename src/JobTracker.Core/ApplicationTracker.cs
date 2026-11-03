namespace JobTracker;

public sealed class ApplicationTracker
{
    private readonly ApplicationStore store;
    private List<JobApplication> applications;
    public IReadOnlyList<JobApplication> Applications => applications.AsReadOnly();

    public ApplicationTracker(ApplicationStore store)
    {
        this.store = store;
        applications = store.Load();
    }

    public JobApplication Add(string company, string role, DateOnly appliedOn, string notes = "",
        ApplicationStatus status = ApplicationStatus.Applied, DateOnly? interviewOn = null)
    {
        if (string.IsNullOrWhiteSpace(company) || string.IsNullOrWhiteSpace(role))
            throw new ArgumentException("Company and role are required.");
        if (appliedOn == default) throw new ArgumentException("An application date is required.");
        ValidateDatesAndStatus(appliedOn, status, interviewOn);
        int id = applications.Count == 0 ? 1 : checked(applications.Max(a => a.Id) + 1);
        var application = new JobApplication(id, company.Trim(), role.Trim(), appliedOn, status, interviewOn, notes.Trim());
        Commit([.. applications, application]);
        return application;
    }

    public void Update(int id, ApplicationStatus status, DateOnly? interviewOn, string notes)
    {
        if (!Enum.IsDefined(status)) throw new ArgumentException("Choose a valid status.");
        var existing = applications.SingleOrDefault(a => a.Id == id)
            ?? throw new ArgumentException("No application has that ID.");
        if (interviewOn < existing.AppliedOn)
            throw new ArgumentException("Interview date cannot be earlier than the application date.");
        var updated = existing with { Status = status, InterviewOn = interviewOn, Notes = notes.Trim() };
        Commit(applications.Select(a => a.Id == id ? updated : a).ToList());
    }

    public void UpdateDetails(int id, string company, string role, DateOnly appliedOn,
        ApplicationStatus status, DateOnly? interviewOn, string notes)
    {
        if (string.IsNullOrWhiteSpace(company) || string.IsNullOrWhiteSpace(role))
            throw new ArgumentException("Company and role are required.");
        if (!applications.Any(a => a.Id == id)) throw new ArgumentException("No application has that ID.");
        ValidateDatesAndStatus(appliedOn, status, interviewOn);
        var updated = new JobApplication(id, company.Trim(), role.Trim(), appliedOn, status, interviewOn, notes.Trim());
        Commit(applications.Select(a => a.Id == id ? updated : a).ToList());
    }

    private static void ValidateDatesAndStatus(DateOnly appliedOn, ApplicationStatus status, DateOnly? interviewOn)
    {
        if (appliedOn == default) throw new ArgumentException("An application date is required.");
        if (!Enum.IsDefined(status)) throw new ArgumentException("Choose a valid status.");
        if (interviewOn < appliedOn) throw new ArgumentException("Interview date cannot be earlier than the application date.");
    }

    public IEnumerable<JobApplication> Search(string text) => applications.Where(a =>
        a.Company.Contains(text, StringComparison.OrdinalIgnoreCase)
        || a.Role.Contains(text, StringComparison.OrdinalIgnoreCase));

    private void Commit(List<JobApplication> next)
    {
        store.Save(next);
        applications = next; // Only publish changes once saving succeeds.
    }
}
