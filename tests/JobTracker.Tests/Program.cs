using System.Text.Json;
using JobTracker;

// A tiny dependency-free test runner. A failing check gives a non-zero exit code.
int failed = 0;
var date = new DateOnly(2026, 11, 3);
Run("Missing file starts empty", path => Equal(0, Open(path).Applications.Count));
Run("Add trims input, assigns IDs, and persists", path =>
{
    var tracker = Open(path);
    Equal(1, tracker.Add(" Example Co ", " Developer ", date).Id);
    Equal(2, tracker.Add("Other Co", "Intern", date).Id);
    var reloaded = Open(path);
    Equal(2, reloaded.Applications.Count);
    Equal("Example Co", reloaded.Applications[0].Company);
    Equal("Developer", reloaded.Applications[0].Role);
    Equal(date, reloaded.Applications[0].AppliedOn);
});
Run("Blank fields are rejected without saving", path =>
{
    var tracker = Open(path);
    Throws<ArgumentException>(() => tracker.Add(" ", "Developer", date));
    Throws<ArgumentException>(() => tracker.Add("Company", "", date));
    Throws<ArgumentException>(() => tracker.Add("Company", "Role", default));
    Equal(0, tracker.Applications.Count);
    Equal(false, File.Exists(path));
});
Run("Status, interview, and notes survive a reload", path =>
{
    var tracker = Open(path);
    var item = tracker.Add("Example", "Co-op", date);
    tracker.Update(item.Id, ApplicationStatus.Interviewing, date.AddDays(7), " Prepare examples ");
    var saved = Open(path).Applications.Single();
    Equal(ApplicationStatus.Interviewing, saved.Status);
    Equal<DateOnly?>(date.AddDays(7), saved.InterviewOn);
    Equal("Prepare examples", saved.Notes);
    tracker.Update(item.Id, ApplicationStatus.Rejected, null, "");
    Equal<DateOnly?>(null, Open(path).Applications.Single().InterviewOn);
});
Run("Invalid updates do not change saved data", path =>
{
    var tracker = Open(path);
    tracker.Add("Example", "Intern", date);
    var original = File.ReadAllText(path);
    Throws<ArgumentException>(() => tracker.Update(99, ApplicationStatus.Offer, null, ""));
    Throws<ArgumentException>(() => tracker.Update(1, (ApplicationStatus)999, null, ""));
    Throws<ArgumentException>(() => tracker.Update(1, ApplicationStatus.Interviewing, date.AddDays(-1), ""));
    Equal(original, File.ReadAllText(path));
});
Run("Search matches company or role without case sensitivity", path =>
{
    var tracker = Open(path);
    tracker.Add("Acme", "Developer", date);
    tracker.Add("Example", "Designer", date);
    Equal(1, tracker.Search("ACME").Count());
    Equal(1, tracker.Search("develop").Count());
    Equal(0, tracker.Search("missing").Count());
});
Run("Malformed JSON is preserved", path =>
{
    File.WriteAllText(path, "broken JSON");
    Throws<JsonException>(() => Open(path));
    Equal("broken JSON", File.ReadAllText(path));
});
Run("Null data is rejected", path =>
{
    File.WriteAllText(path, "null");
    Throws<InvalidDataException>(() => Open(path));
});
Run("Invalid records and duplicate IDs are rejected", path =>
{
    var store = new ApplicationStore(path);
    var valid = new JobApplication(1, "Company", "Role", date);
    foreach (var bad in new[] { valid with { Company = "" }, valid with { Id = 0 },
        valid with { AppliedOn = default }, valid with { InterviewOn = date.AddDays(-1) } })
    {
        store.Save([bad]);
        Throws<InvalidDataException>(() => store.Load());
    }
    store.Save([valid, valid]);
    Throws<InvalidDataException>(() => store.Load());
});
Run("A failed save leaves in-memory state unchanged", path =>
{
    var tracker = Open(path);
    tracker.Add("Original", "Role", date);
    File.Delete(path);
    Directory.CreateDirectory(path); // A directory cannot be replaced by the JSON file.
    try { tracker.Add("Unsaved", "Role", date); throw new Exception("Expected a save failure."); }
    catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
    Equal(1, tracker.Applications.Count);
    Equal(0, Directory.GetFiles(Path.GetDirectoryName(path)!, "*.tmp").Length);
});
Console.WriteLine($"\n{(failed == 0 ? "All checks passed." : $"{failed} check(s) failed.")}");
return failed == 0 ? 0 : 1;

ApplicationTracker Open(string path) => new(new ApplicationStore(path));

void Run(string name, Action<string> test)
{
    string directory = Path.Combine(Path.GetTempPath(), "JobTracker.Tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    try { test(Path.Combine(directory, "applications.json")); Console.WriteLine($"PASS {name}"); }
    catch (Exception error) { failed++; Console.WriteLine($"FAIL {name}: {error.Message}"); }
    finally { Directory.Delete(directory, recursive: true); }
}

void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new Exception($"Expected '{expected}', got '{actual}'.");
}

void Throws<T>(Action action) where T : Exception
{
    try { action(); }
    catch (T) { return; }
    throw new Exception($"Expected {typeof(T).Name}.");
}
