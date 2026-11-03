using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using JobTracker.ViewModels;
using JobTracker.Views;

namespace JobTracker;

public partial class App : Application
{
    private FileStream? dataLock;
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var args = desktop.Args ?? [];
            bool demo = args.Contains("--demo");
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "JobTracker", "applications.json");
            string? startupError = null;
            if (demo) path = Path.Combine(Path.GetTempPath(), "JobTracker-Demo", Guid.NewGuid().ToString("N"), "applications.json");
            else if (args is ["--data", var customPath]) path = customPath;
            else if (args.Length != 0) startupError = "Unknown launch option. Use --demo or --data <file>.";
            try
            {
                if (startupError is null)
                {
                    path = Path.GetFullPath(path);
                    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
                    // The handle, not the presence of the file, owns the lock. Closing releases it.
                    dataLock = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                    if (demo) DemoData.Create(path);
                }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
            {
                startupError = "Couldn't open this workspace. Close any other copy of JobTracker and check folder permissions. " + error.Message;
            }
            var vm = new MainViewModel(path, demo: demo, startupError: startupError);
            var window = new MainWindow { DataContext = vm };
            desktop.MainWindow = window;
            desktop.Exit += (_, _) =>
            {
                dataLock?.Dispose();
                if (demo)
                {
                    try { Directory.Delete(Path.GetDirectoryName(path)!, recursive: true); }
                    catch (Exception error) when (error is IOException or UnauthorizedAccessException) { /* OS temp cleanup can finish later. */ }
                }
            };
        }
        base.OnFrameworkInitializationCompleted();
    }
}
