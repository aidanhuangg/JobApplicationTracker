namespace JobTracker;

public enum ApplicationStatus { Applied, Interviewing, Offer, Rejected, Withdrawn }

// A record describes one saved application. Dates intentionally have no time zone.
public sealed record JobApplication(
    int Id,
    string Company,
    string Role,
    DateOnly AppliedOn,
    ApplicationStatus Status = ApplicationStatus.Applied,
    DateOnly? InterviewOn = null,
    string Notes = "");
