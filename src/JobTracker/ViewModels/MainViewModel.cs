using System.Collections.ObjectModel;
using System.Text.Json;

namespace JobTracker.ViewModels;

// This class owns screen state. It knows no Avalonia controls and can be tested without a window.
public sealed class MainViewModel : ObservableObject
{
    private readonly ApplicationStore store;
    private readonly Func<DateOnly> today;
    private ApplicationTracker? tracker;
    private string search = "", filter = "All statuses", view = "all", notification = "", loadError = "";
    private ApplicationRow? selected;
    private ApplicationEditor? editor;
    private bool discardPrompt;
    public string DataPath { get; }
    public bool IsDemo { get; }
    public string WorkspaceLabel => IsDemo ? "DEMO WORKSPACE" : "PERSONAL WORKSPACE";
    public string FooterLabel => IsDemo ? "Sample data · resets on exit" : "Saved locally on this device";
    public ObservableCollection<ApplicationRow> Applications { get; } = [];
    public string[] StatusFilters { get; } = ["All statuses", .. Enum.GetNames<ApplicationStatus>()];
    public RelayCommand NewCommand { get; }
    public RelayCommand EditCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand DiscardCommand { get; }
    public RelayCommand KeepEditingCommand { get; }
    public RelayCommand AllCommand { get; }
    public RelayCommand UpcomingCommand { get; }
    public RelayCommand OffersCommand { get; }
    public RelayCommand ClearFiltersCommand { get; }
    public RelayCommand DismissCommand { get; }
    public bool CanUseWorkspace => tracker is not null;
    public string LoadError { get => loadError; private set { if (Set(ref loadError, value)) Raise(nameof(HasLoadError)); } }
    public bool HasLoadError => LoadError.Length > 0;
    public string Notification { get => notification; private set { if (Set(ref notification, value)) Raise(nameof(HasNotification)); } }
    public bool HasNotification => Notification.Length > 0;
    public string Search { get => search; set { if (Set(ref search, value)) Refresh(); } }
    public string Filter { get => filter; set { if (Set(ref filter, value)) Refresh(); } }
    public ApplicationRow? Selected
    {
        get => selected;
        set { if (Set(ref selected, value)) { Raise(nameof(HasSelection)); EditCommand.Refresh(); } }
    }
    public bool HasSelection => Selected is not null;
    public ApplicationEditor? Editor
    {
        get => editor;
        private set { if (Set(ref editor, value)) Raise(nameof(IsEditorOpen)); }
    }
    public bool IsEditorOpen => Editor is not null;
    public bool DiscardPrompt { get => discardPrompt; private set => Set(ref discardPrompt, value); }
    public bool IsAllView => view == "all";
    public bool IsUpcomingView => view == "upcoming";
    public bool IsOffersView => view == "offers";
    public string PageTitle => view switch { "upcoming" => "Upcoming interviews", "offers" => "Your offers", _ => "Your applications" };
    public string PageSubtitle => view switch
    {
        "upcoming" => "A little preparation. A confident next step.",
        "offers" => "Good news deserves a place of its own.",
        _ => "A little more organized. A little closer to your next chapter."
    };
    public int TotalCount => tracker?.Applications.Count ?? 0;
    public int ActiveCount => tracker?.Applications.Count(a => a.Status is ApplicationStatus.Applied or ApplicationStatus.Interviewing) ?? 0;
    public int InterviewCount => tracker?.Applications.Count(a => a.Status == ApplicationStatus.Interviewing) ?? 0;
    public int OfferCount => tracker?.Applications.Count(a => a.Status == ApplicationStatus.Offer) ?? 0;
    public string ResultCount => $"{Applications.Count} {(Applications.Count == 1 ? "application" : "applications")}";
    public bool IsEmpty => Applications.Count == 0 && CanUseWorkspace;
    public string EmptyTitle => TotalCount == 0 ? "Your next chapter starts here" : "Nothing here just yet";
    public string EmptyMessage => TotalCount == 0 ? "Add your first application. Keep every opportunity,\ninterview, and next step in one calm place." : "Try another search or status, or return to all applications.";
    public bool HasApplications => TotalCount > 0;

    public MainViewModel(string path, Func<DateOnly>? today = null, bool demo = false, string? startupError = null)
    {
        DataPath = path;
        IsDemo = demo;
        this.today = today ?? (() => DateOnly.FromDateTime(DateTime.Today));
        store = new ApplicationStore(path);
        NewCommand = new(() => OpenEditor(null), () => CanUseWorkspace);
        EditCommand = new(() => OpenEditor(Selected?.Model), () => CanUseWorkspace && HasSelection);
        SaveCommand = new(Save);
        CancelCommand = new(Cancel);
        DiscardCommand = new(() => { Editor = null; DiscardPrompt = false; });
        KeepEditingCommand = new(() => DiscardPrompt = false);
        AllCommand = new(() => SetView("all"));
        UpcomingCommand = new(() => SetView("upcoming"));
        OffersCommand = new(() => SetView("offers"));
        ClearFiltersCommand = new(() => SetView("all"));
        DismissCommand = new(() => Notification = "");
        if (startupError is not null) LoadError = startupError;
        else
        {
            try { tracker = new ApplicationTracker(store); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
            { LoadError = "Couldn't load saved applications. Your file is unchanged. Check the file below and reopen the app. " + error.Message; }
        }
        Refresh();
    }

    private void SetView(string next)
    {
        view = next;
        search = "";
        filter = "All statuses";
        foreach (string name in new[] { nameof(IsAllView), nameof(IsUpcomingView), nameof(IsOffersView), nameof(PageTitle), nameof(PageSubtitle), nameof(Search), nameof(Filter) }) Raise(name);
        Refresh();
    }

    private void Refresh(int? selectId = null)
    {
        int? id = selectId ?? Selected?.Id;
        var rows = (tracker?.Applications ?? []).Where(a =>
            (string.IsNullOrWhiteSpace(Search) || a.Company.Contains(Search.Trim(), StringComparison.OrdinalIgnoreCase) || a.Role.Contains(Search.Trim(), StringComparison.OrdinalIgnoreCase))
            && (Filter == "All statuses" || a.Status.ToString() == Filter))
            .Select(a => new ApplicationRow(a, today()))
            .Where(a => view != "upcoming" || a.IsUpcoming)
            .Where(a => view != "offers" || a.Model.Status == ApplicationStatus.Offer);
        rows = view == "upcoming" ? rows.OrderBy(a => a.Model.InterviewOn) : rows.OrderByDescending(a => a.Model.AppliedOn).ThenByDescending(a => a.Id);
        var list = rows.ToList();
        Applications.Clear();
        foreach (var row in list) Applications.Add(row);
        Selected = list.FirstOrDefault(a => a.Id == id) ?? list.FirstOrDefault();
        foreach (string name in new[] { nameof(TotalCount), nameof(ActiveCount), nameof(InterviewCount), nameof(OfferCount), nameof(ResultCount), nameof(IsEmpty), nameof(HasApplications), nameof(EmptyTitle), nameof(EmptyMessage), nameof(CanUseWorkspace) }) Raise(name);
        NewCommand.Refresh();
    }

    private void OpenEditor(JobApplication? application)
    {
        DiscardPrompt = false;
        Editor = new ApplicationEditor(application, today());
    }

    private void Cancel()
    {
        if (Editor?.IsDirty == true) DiscardPrompt = true;
        else Editor = null;
    }

    private void Save()
    {
        if (Editor is null || tracker is null) return;
        var form = Editor;
        if (!form.Validate()) return;
        try
        {
            var applied = DateOnly.FromDateTime(form.AppliedDate!.Value);
            DateOnly? interview = form.InterviewDate.HasValue ? DateOnly.FromDateTime(form.InterviewDate.Value) : null;
            int id;
            if (form.Id is int existing)
            {
                tracker.UpdateDetails(existing, form.Company, form.Role, applied, form.Status, interview, form.Notes);
                id = existing;
            }
            else id = tracker.Add(form.Company, form.Role, applied, form.Notes, form.Status, interview).Id;
            Editor = null;
            DiscardPrompt = false;
            // Show the saved record even when it no longer matches the previous filter.
            SetView("all");
            Refresh(id);
            Notification = $"{form.Company.Trim()} application saved.";
        }
        catch (Exception error) when (error is ArgumentException or IOException or UnauthorizedAccessException or OverflowException)
        { form.Error = "Couldn't save. " + error.Message + " Your changes are still here; try again."; }
    }
}
