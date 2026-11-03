# Job Application Tracker

A small, beginner-friendly **C# / .NET console application** for keeping track of your job search. No database, accounts, web server, or third-party packages are needed.

## What you can do

- Add a company, role, application date, and notes.
- List applications and see counts by status.
- Update status: Applied, Interviewing, Offer, Rejected, or Withdrawn.
- Record or clear an interview date and edit notes.
- Search company names or roles without worrying about capitalization.
- View upcoming interviews for active applications, sorted by date.
- Automatically save changes to a local JSON file and reload them next time.

## Windows setup and run

1. Install the **.NET 10 SDK** from [Microsoft](https://dotnet.microsoft.com/en-us/download/dotnet/10.0). Choose the SDK rather than just the runtime. .NET 10 is an [LTS release](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core).
2. Extract this project ZIP. Open the `JobApplicationTracker` folder in File Explorer, right-click inside it, and select **Open in Terminal**.
3. Check the SDK, then run the app:

```powershell
dotnet --version
dotnet run --project src/JobTracker
```

The version should start with `10.` (or use a newer SDK that supports .NET 10). Use the menu numbers and press Enter. Choose `0` to quit. You can also open `JobApplicationTracker.sln` in a .NET 10-compatible Visual Studio installation with the **.NET desktop development** workload, set `JobTracker` as the startup project, and press Ctrl+F5.

This console app also runs on macOS and Linux with the .NET 10 SDK using the same commands.

## Try it in two minutes

1. Choose `2` to add an application. Enter `Example Software`, `Developer Co-op`, an application date such as `2026-11-03`, and a short note.
2. Choose `1` to list it and find its ID.
3. Choose `3`, enter the ID, choose `2` for Interviewing, and enter an interview date on or after the application date.
4. Choose `5` to see full details, or `6` to list interviews happening today or later. Past interview dates stay in the record but do not appear in the upcoming list.
5. Quit and reopen the app. Your application will still be there.

Dates use `YYYY-MM-DD`. When updating, Enter keeps the existing value; `-` clears the interview date or notes. The upcoming list excludes Offer, Rejected, and Withdrawn applications. Status is updated manually; entering an interview date does not automatically change it.

Example list (illustrative):

```text
ID    COMPANY                ROLE                         STATUS        APPLIED    INTERVIEW
----------------------------------------------------------------------------------------------------
1     Example Software       Developer Co-op              Interviewing  2026-11-03 2026-11-10
```

## Where your data goes

On Windows the default is `%LOCALAPPDATA%\JobTracker\applications.json`. The app prints the full location when it starts. The file is created when you first add an application; restarting from a different folder still uses the same default file.

To use another file, including a separate practice file:

```powershell
dotnet run --project src/JobTracker -- --data ./practice-applications.json
```

Changes are written to a temporary file in the same folder and then moved over the previous file. Failed saves do not change the in-memory list. Invalid or unreadable existing data produces an error and is not silently replaced. To recover from malformed data, close the app, copy the file for safekeeping, then fix the JSON or move it aside to start fresh. Back up the file periodically. Data is plain text, stored locally, and has no encryption or cloud sync.

Run **one instance per data file**. Simultaneous instances are not supported and can overwrite each other's changes. There is no deletion feature; the app focuses on a small set of easy-to-understand operations.

## How the code fits together

```text
JobApplicationTracker.sln
src/JobTracker/
  Program.cs              Console menus, input parsing, and display
  JobApplication.cs       Application record and status enum
  ApplicationTracker.cs  Adding, updating, searching, and validation
  ApplicationStore.cs    Loading and safely replacing the JSON file
tests/JobTracker.Tests/
  Program.cs              Dependency-free automated checks
```

Start reading `JobApplication.cs`, then `ApplicationTracker.cs`, then `Program.cs`. The application record holds data. The tracker enforces rules and asks the store to save it. The console handles interaction. Separating these responsibilities lets you test the logic without typing into the menu.

Useful C# concepts demonstrated:

- **Records and enums** describe an application and its allowed statuses.
- **Collections and LINQ** handle listing, filtering, ordering, and summary counts.
- **Methods and classes** separate display, application rules, and file storage.
- **Exceptions and validation** handle bad input and file errors.
- **System.Text.Json** saves and reloads structured data.

This version uses a simple console interface. A future desktop UI could reuse the application and storage classes.

## Build and checks

Run from the project root:

```powershell
dotnet build JobApplicationTracker.sln --configuration Release
dotnet run --project tests/JobTracker.Tests --configuration Release
```

The test project is a small executable with no test-framework dependencies. Use `dotnet run` above, rather than `dotnet test`. It prints PASS/FAIL for each case and returns a nonzero exit code on failure. Tests use isolated temporary files and remove them afterward; they never use your real application file.

Checks cover adding and reloading records, ID assignment, required fields, updating and clearing fields, invalid statuses and dates, searches, malformed data, duplicate IDs, and failed-save behavior. A GitHub Actions workflow runs the build and checks on Windows.

Validated during delivery: Release build with zero warnings, all 10 automated checks, and a console walkthrough covering add, list, update, search, details, upcoming interviews, restart, and invalid input. These checks ran on macOS with .NET 10; Windows execution was not available locally.

`NuGet.Config` clears package sources because this project needs no external packages. If you later add a package, restore the NuGet.org package source first.

## Ideas to try next

Add a status filter, export to CSV, track multiple interview rounds, or add a confirmed delete action. Start with one feature at a time and add a check for its behavior.

## Troubleshooting

- **`dotnet` is not recognized:** install the SDK and reopen Terminal.
- **SDK does not support .NET 10:** install the .NET 10 SDK, then check `dotnet --list-sdks`.
- **Cannot open/save data:** check the printed file path and folder permissions; try `--data` with a writable folder.
- **No upcoming interviews:** ensure the interview date is today or later and the application's status is Applied or Interviewing.

This is an independent learning project, with no connection to any employer.
