namespace JobTracker;

public static class DemoData
{
    public static void Create(string path)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        new ApplicationStore(path).Save([
            new(1, "Linear", "Software Engineer Intern", today.AddDays(-8), ApplicationStatus.Interviewing, today.AddDays(2), "Portfolio conversation with the product team. Prepare a walkthrough of a project you enjoyed building."),
            new(2, "Notion", "Developer Co-op", today.AddDays(-5), ApplicationStatus.Applied, Notes: "Applied through the careers page. Interested in the collaboration team."),
            new(3, "Figma", "Frontend Engineering Intern", today.AddDays(-12), ApplicationStatus.Offer, Notes: "Offer received. Review the details and follow up with the recruiter."),
            new(4, "Arc Studio", "C# Developer Intern", today.AddDays(-3), ApplicationStatus.Interviewing, today.AddDays(5), "Technical interview. Review collections, testing, and desktop UI patterns."),
            new(5, "Northstar", "Junior Software Developer", today.AddDays(-2), ApplicationStatus.Applied, Notes: "Referred by a classmate. Keep an eye on email for next steps."),
            new(6, "Vercel", "Engineering Intern", today.AddDays(-16), ApplicationStatus.Rejected, Notes: "Closed this round. Keep building and try again next term.")
        ]);
    }
}
