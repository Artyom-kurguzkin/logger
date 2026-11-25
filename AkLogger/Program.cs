using AkLogger.AppState;
using AkLogger.Components.Pages.Home;
using AkLogger.Sdk;
using Microsoft.Extensions.DependencyInjection;
using Photino.Blazor;
using Radzen;

namespace AkLogger;

/// <summary>Composition root: wires the services, registers the built-in extensions and owns the one window.</summary>
internal static class Program
{
    /// <summary>The built-in features, each one an extension (the registry is the only way a view reaches the UI). A new feature is one more entry.</summary>
    private static readonly IExtension[] BuiltIns = [new HomeExtension()];

    [STAThread]
    private static void Main(string[] args)
    {
        // ponytail: a crash line in a file and nothing else (no run log, watchdog or message box); grow it when a crash needs more than a stack trace
        AppDomain.CurrentDomain.UnhandledException += (_, e) => LogCrash("Unhandled exception" + (e.IsTerminating ? "; it ends the process" : "") + ":\n" + e.ExceptionObject);
        TaskScheduler.UnobservedTaskException += (_, e) => { LogCrash("A task's exception was never observed:\n" + e.Exception); e.SetObserved(); };

        PhotinoBlazorAppBuilder builder = PhotinoBlazorAppBuilder.CreateDefault(args);
        builder.Services.AddRadzenComponents();   // Dropdown is a RadzenDropDown
        builder.Services.AddSingleton(new Extensions(BuiltIns));
        builder.Services.AddSingleton<UiService>();
        builder.Services.AddSingleton(new SettingsStore(Environment.GetEnvironmentVariable("AK_SETTINGS") ?? SettingsStore.DefaultPath));   // AK_SETTINGS: a test run's own file
        builder.RootComponents.Add<App>("#app");

        PhotinoBlazorApp app = builder.Build();
        app.MainWindow.SetTitle("ak-logger").SetUseOsDefaultSize(true).SetMaximized(true);
        app.MainWindow.SetLogVerbosity(0);   // Photino prints every message it sends to the page to stdout
        app.Run();
    }

    /// <summary>Appends to %LOCALAPPDATA%/AkLogger/logs/crash-yyyy-MM-dd.log; a log that cannot be written changes nothing.</summary>
    private static void LogCrash(string text)
    {
        try
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AkLogger", "logs");
            Directory.CreateDirectory(folder);
            File.AppendAllText(Path.Combine(folder, $"crash-{DateTime.Now:yyyy-MM-dd}.log"), $"{DateTime.Now:HH:mm:ss} {text}\n");
        }
        catch (Exception) { }
    }
}
