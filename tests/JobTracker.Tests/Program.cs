using System.Text.Json;
using JobTracker;
using JobTracker.ViewModels;
using JobTracker.Views;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Automation;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Media.Imaging;

AppBuilder.Configure<App>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).WithInterFont().SetupWithoutStarting();

// A tiny test runner; Avalonia.Headless exercises the actual window without a display. A failing check gives a non-zero exit code.
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
Run("Full editor updates company, role, and dates atomically", path =>
{
    var tracker = Open(path);
    tracker.Add("Old", "Old role", date);
    tracker.UpdateDetails(1, "New", "New role", date.AddDays(1), ApplicationStatus.Offer, date.AddDays(2), "Accepted next steps");
    var saved = Open(path).Applications.Single();
    Equal("New", saved.Company);
    Equal("New role", saved.Role);
    Equal(date.AddDays(1), saved.AppliedOn);
    var original = File.ReadAllText(path);
    Throws<ArgumentException>(() => tracker.UpdateDetails(1, "New", "", date, ApplicationStatus.Applied, null, ""));
    Equal(original, File.ReadAllText(path));
});
Run("New form validates required fields and interview chronology", path =>
{
    var vm = new MainViewModel(path, () => date);
    vm.NewCommand.Execute(null);
    vm.SaveCommand.Execute(null);
    Equal(true, vm.Editor!.HasError);
    Equal(0, vm.TotalCount);
    vm.Editor.Company = "Acme";
    vm.Editor.Role = "Developer";
    vm.Editor.InterviewDate = date.AddDays(-1).ToDateTime(TimeOnly.MinValue);
    vm.SaveCommand.Execute(null);
    Equal(true, vm.IsEditorOpen);
    vm.Editor.InterviewDate = date.ToDateTime(TimeOnly.MinValue);
    vm.Editor.Status = ApplicationStatus.Interviewing;
    vm.SaveCommand.Execute(null);
    Equal(false, vm.IsEditorOpen);
    Equal(1, vm.InterviewCount);
    Equal(1, Open(path).Applications.Count);
});
Run("Search and status filters combine and keep details in sync", path =>
{
    var tracker = Open(path);
    tracker.Add("Acme", "Developer", date);
    tracker.Add("Acme", "Designer", date, status: ApplicationStatus.Offer);
    tracker.Add("Other", "Engineer", date);
    var vm = new MainViewModel(path, () => date) { Search = "ACME", Filter = "Offer" };
    Equal(1, vm.Applications.Count);
    Equal("Designer", vm.Selected!.Role);
    vm.Search = "missing";
    Equal(true, vm.IsEmpty);
    Equal(false, vm.HasSelection);
    vm.ClearFiltersCommand.Execute(null);
    Equal(3, vm.Applications.Count);
    Equal(1, vm.OfferCount);
});
Run("Upcoming excludes past and closed interviews, sorts soonest first", path =>
{
    var tracker = Open(path);
    var applied = date.AddDays(-10);
    tracker.Add("Later", "Role", applied, status: ApplicationStatus.Interviewing, interviewOn: date.AddDays(4));
    tracker.Add("Today", "Role", applied, interviewOn: date);
    tracker.Add("Past", "Role", applied, interviewOn: date.AddDays(-1));
    tracker.Add("Closed", "Role", applied, status: ApplicationStatus.Rejected, interviewOn: date.AddDays(1));
    var vm = new MainViewModel(path, () => date);
    vm.UpcomingCommand.Execute(null);
    Equal(2, vm.Applications.Count);
    Equal("Today", vm.Applications.First().Company);
});
Run("Cancel protects dirty forms; discard does not save", path =>
{
    var vm = new MainViewModel(path, () => date);
    vm.NewCommand.Execute(null);
    vm.CancelCommand.Execute(null);
    Equal(false, vm.IsEditorOpen);
    vm.NewCommand.Execute(null);
    vm.Editor!.Company = "Unsaved";
    vm.CancelCommand.Execute(null);
    Equal(true, vm.DiscardPrompt);
    vm.KeepEditingCommand.Execute(null);
    Equal(true, vm.IsEditorOpen);
    Equal(false, vm.DiscardPrompt);
    vm.DiscardCommand.Execute(null);
    Equal(false, vm.IsEditorOpen);
    Equal(false, File.Exists(path));
});
Run("Saved edits reset filters so the application remains visible", path =>
{
    Open(path).Add("Company", "Role", date);
    var vm = new MainViewModel(path, () => date) { Filter = "Applied" };
    vm.EditCommand.Execute(null);
    vm.Editor!.Status = ApplicationStatus.Offer;
    vm.Editor.Company = "Renamed";
    vm.SaveCommand.Execute(null);
    Equal("Renamed", vm.Selected!.Company);
    Equal("Offer", vm.Selected.Status);
    Equal("All statuses", vm.Filter);
    Equal(1, vm.OfferCount);
});
Run("Load errors disable editing and preserve the corrupt file", path =>
{
    File.WriteAllText(path, "null");
    var vm = new MainViewModel(path, () => date);
    Equal(true, vm.HasLoadError);
    Equal(false, vm.NewCommand.CanExecute(null));
    Equal("null", File.ReadAllText(path));
});
Run("Failed saves retain the form and previous summary", path =>
{
    var vm = new MainViewModel(path, () => date);
    vm.NewCommand.Execute(null);
    vm.Editor!.Company = "Company";
    vm.Editor.Role = "Role";
    Directory.CreateDirectory(path);
    vm.SaveCommand.Execute(null);
    Equal(true, vm.IsEditorOpen);
    Equal(true, vm.Editor.HasError);
    Equal("Company", vm.Editor.Company);
    Equal(0, vm.TotalCount);
});
Run("Actual XAML window binds search, form input, save, and results", path =>
{
    var vm = new MainViewModel(path, () => date);
    var window = new MainWindow { DataContext = vm };
    window.Show();
    try
    {
        var add = window.GetVisualDescendants().OfType<Button>().First(b => b.Command == vm.NewCommand);
        add.Focus();
        window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        Equal(true, vm.IsEditorOpen);
        var company = window.GetVisualDescendants().OfType<TextBox>().Single(b => AutomationProperties.GetName(b) == "Company");
        var role = window.GetVisualDescendants().OfType<TextBox>().Single(b => AutomationProperties.GetName(b) == "Job title");
        company.Text = "UI Company";
        role.Text = "UI Developer";
        Dispatcher.UIThread.RunJobs();
        Equal("UI Company", vm.Editor!.Company);
        var save = window.GetVisualDescendants().OfType<Button>().Single(b => b.Command == vm.SaveCommand);
        save.Focus();
        window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        Equal(false, vm.IsEditorOpen);
        Equal(1, vm.TotalCount);
        var list = window.FindControl<ListBox>("ApplicationList")!;
        Equal(1, list.ItemCount);
        window.FindControl<TextBox>("SearchBox")!.Text = "no match";
        Dispatcher.UIThread.RunJobs();
        Equal(0, list.ItemCount);
        Equal(true, vm.IsEmpty);
    }
    finally { vm.DiscardCommand.Execute(null); window.Close(); }
});
Run("Demo mode uses a separate file and contains the full status range", path =>
{
    DemoData.Create(path);
    var vm = new MainViewModel(path, demo: true);
    Equal(true, vm.IsDemo);
    Equal(6, vm.TotalCount);
    Equal(2, vm.InterviewCount);
    Equal(1, vm.OfferCount);
    vm.UpcomingCommand.Execute(null);
    Equal(2, vm.Applications.Count);
});
if (args is ["--screenshots", var output])
{
    Run("Render dashboard and editor previews from actual XAML", path =>
    {
        Directory.CreateDirectory(output);
        DemoData.Create(path);
        var vm = new MainViewModel(path, demo: true);
        vm.Selected = vm.Applications.Single(a => a.Company == "Linear");
        var window = new MainWindow { DataContext = vm, Width = 1440, Height = 900 };
        window.Show();
        try
        {
            Capture("dashboard.png");
            vm.EditCommand.Execute(null);
            Capture("editor.png");
            vm.DiscardCommand.Execute(null);
            window.Width = 1120;
            window.Height = 740;
            Capture("compact.png");
        }
        finally { vm.DiscardCommand.Execute(null); window.Close(); }
        void Capture(string name)
        {
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            using var image = new RenderTargetBitmap(new PixelSize((int)window.Width, (int)window.Height));
            image.Render(window);
            image.Save(Path.Combine(output, name), PngBitmapEncoderOptions.Default);
        }
    });
}
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
