using AkLogger.Sdk;

namespace AkLogger.Components.Pages.Home;

/// <summary>The first built-in extension: the start page (Logs) and Settings. Proves a view reaches the app only through the registry.</summary>
public sealed class HomeExtension : IExtension
{
    public void Register(IExtensionHost host)
    {
        host.AddView(new ViewDescriptor("logs", "Logs", "M6 4h8M6 8h8M6 12h8M2.5 4h.5M2.5 8h.5M2.5 12h.5", "", typeof(LogsPage), Order: 0));
        host.AddView(new ViewDescriptor("settings", "Settings", "M2 4.5h7M13 4.5h1M2 11.5h2M8 11.5h6M11 2.7a1.8 1.8 0 1 0 0 3.6a1.8 1.8 0 1 0 0-3.6M6 9.7a1.8 1.8 0 1 0 0 3.6a1.8 1.8 0 1 0 0-3.6", "settings", typeof(SettingsPage), Order: 100));
    }
}
