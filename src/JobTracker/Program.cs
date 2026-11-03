using System.Globalization;
using System.Text.Json;
using JobTracker;

if (args is ["--help"])
{
    Console.WriteLine("JobTracker: dotnet run --project src/JobTracker [-- --data <path>]");
    Console.WriteLine("Run one instance per data file. Changes save automatically.");
    return 0;
}
if (args.Length != 0 && (args.Length != 2 || args[0] != "--data" || string.IsNullOrWhiteSpace(args[1])))
{
    Console.Error.WriteLine("Usage: JobTracker [--data <path>] or JobTracker --help");
    return 1;
}

string path = args.Length == 2 ? Path.GetFullPath(args[1]) : Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "JobTracker", "applications.json");

try
{
    var tracker = new ApplicationTracker(new ApplicationStore(path));
    Console.WriteLine("\nJOB APPLICATION TRACKER");
    Console.WriteLine($"Data: {path}");
    Console.WriteLine("All changes save automatically. Dates use YYYY-MM-DD.\n");
    while (true)
    {
        PrintSummary(tracker);
        Console.WriteLine("1 List all   2 Add   3 Update   4 Search   5 Details   6 Upcoming interviews   0 Quit");
        try
        {
            switch (Read("Choose"))
            {
                case "0": return 0;
                case "1": PrintList(tracker.Applications); break;
                case "2":
                    string company = Required("Company");
                    string role = Required("Role");
                    DateOnly applied = ReadDate("Applied on (Enter = today)") ?? DateOnly.FromDateTime(DateTime.Today);
                    string notes = Read("Notes (optional)");
                    var added = tracker.Add(company, role, applied, notes);
                    Console.WriteLine($"Saved application #{added.Id}.\n");
                    break;
                case "3":
                    var current = Find(tracker);
                    if (current is null) break;
                    Console.WriteLine($"Updating {current.Company} / {current.Role}");
                    var statuses = Enum.GetValues<ApplicationStatus>();
                    for (int i = 0; i < statuses.Length; i++) Console.WriteLine($"{i + 1} {statuses[i]}");
                    string choice = Read($"Status (Enter keeps {current.Status})");
                    var status = current.Status;
                    if (choice.Length > 0)
                    {
                        if (!int.TryParse(choice, out int index) || index < 1 || index > statuses.Length)
                            throw new ArgumentException("Choose a status number from the list.");
                        status = statuses[index - 1];
                    }
                    string interviewText = Read($"Interview date (current: {FormatDate(current.InterviewOn)}; Enter keeps, - clears)");
                    DateOnly? interview = interviewText switch
                    {
                        "" => current.InterviewOn,
                        "-" => null,
                        _ => ParseDate(interviewText)
                    };
                    string newNotes = Read("Notes (Enter keeps, - clears)");
                    tracker.Update(current.Id, status, interview,
                        newNotes == "" ? current.Notes : newNotes == "-" ? "" : newNotes);
                    Console.WriteLine("Changes saved.\n");
                    break;
                case "4": PrintList(tracker.Search(Required("Search company or role"))); break;
                case "5":
                    var item = Find(tracker);
                    if (item is null) break;
                    Console.WriteLine($"\n#{item.Id}: {item.Company} / {item.Role}");
                    Console.WriteLine($"Applied: {FormatDate(item.AppliedOn)} | Status: {item.Status}");
                    Console.WriteLine($"Interview: {FormatDate(item.InterviewOn)}\nNotes: {item.Notes}\n");
                    break;
                case "6":
                    var today = DateOnly.FromDateTime(DateTime.Today);
                    PrintList(tracker.Applications.Where(a => a.InterviewOn >= today
                        && a.Status is ApplicationStatus.Applied or ApplicationStatus.Interviewing)
                        .OrderBy(a => a.InterviewOn));
                    break;
                default: Console.WriteLine("Choose a menu number.\n"); break;
            }
        }
        catch (ArgumentException error) { Console.WriteLine($"Input error: {error.Message}\n"); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"Could not save: {error.Message}\nChanges were not applied.\n");
        }
    }
}
catch (EndOfStreamException) { Console.WriteLine("\nGoodbye."); return 0; }
catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
{
    Console.Error.WriteLine($"Cannot open application data: {error.Message}");
    Console.Error.WriteLine($"Check {path}. Your existing data has not been overwritten.");
    return 1;
}

static string Read(string prompt)
{
    Console.Write($"{prompt}: ");
    return (Console.ReadLine() ?? throw new EndOfStreamException()).Trim();
}

static string Required(string prompt)
{
    while (true)
    {
        string value = Read(prompt);
        if (value.Length > 0) return value;
        Console.WriteLine("Please enter a value.");
    }
}

static DateOnly ParseDate(string value) =>
    DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
        && date != default ? date : throw new ArgumentException("Use a real date in YYYY-MM-DD format.");

static DateOnly? ReadDate(string prompt)
{
    string value = Read(prompt);
    return value.Length == 0 ? null : ParseDate(value);
}

static string FormatDate(DateOnly? date) => date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "-";

static JobApplication? Find(ApplicationTracker tracker)
{
    if (!int.TryParse(Read("Application ID"), out int id)) throw new ArgumentException("Enter a numeric ID.");
    var result = tracker.Applications.FirstOrDefault(a => a.Id == id);
    if (result is null) Console.WriteLine("No application has that ID.\n");
    return result;
}

static void PrintSummary(ApplicationTracker tracker)
{
    Console.WriteLine($"Total: {tracker.Applications.Count} | " + string.Join(" | ",
        Enum.GetValues<ApplicationStatus>().Select(s => $"{s}: {tracker.Applications.Count(a => a.Status == s)}")));
}

static void PrintList(IEnumerable<JobApplication> source)
{
    var items = source.ToList();
    if (items.Count == 0) { Console.WriteLine("No matching applications.\n"); return; }
    Console.WriteLine($"\n{"ID",-5} {"COMPANY",-22} {"ROLE",-28} {"STATUS",-13} {"APPLIED",-10} INTERVIEW");
    Console.WriteLine(new string('-', 100));
    foreach (var a in items)
        Console.WriteLine($"{a.Id,-5} {Short(a.Company, 22),-22} {Short(a.Role, 28),-28} {a.Status,-13} {FormatDate(a.AppliedOn),-10} {FormatDate(a.InterviewOn)}");
    Console.WriteLine("Use Details to read full names and notes.\n");
}

static string Short(string value, int length) => value.Length <= length ? value : value[..(length - 3)] + "...";
