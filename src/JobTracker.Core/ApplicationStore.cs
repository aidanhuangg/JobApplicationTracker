using System.Text.Json;
using System.Text.Json.Serialization;

namespace JobTracker;

// Keeping file access separate makes the tracking logic easier to test.
public sealed class ApplicationStore(string path)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    public List<JobApplication> Load()
    {
        if (!File.Exists(path)) return [];
        var items = JsonSerializer.Deserialize<List<JobApplication>>(File.ReadAllText(path), Options)
            ?? throw new InvalidDataException("The data file must contain a JSON array.");
        var ids = new HashSet<int>();
        foreach (var item in items)
        {
            if (item is null || item.Id <= 0 || !ids.Add(item.Id)
                || string.IsNullOrWhiteSpace(item.Company) || string.IsNullOrWhiteSpace(item.Role)
                || item.AppliedOn == default || !Enum.IsDefined(item.Status)
                || item.Notes is null || item.InterviewOn < item.AppliedOn)
                throw new InvalidDataException("The data file contains an invalid application or duplicate ID.");
        }
        return items;
    }

    public void Save(IReadOnlyList<JobApplication> applications)
    {
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        // Write beside the target first: a failed write leaves the old file intact.
        var temporaryPath = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(applications, Options));
            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }
}
