# ak-logger

A local log explorer for the desktop: logs, OpenTelemetry traces, graphs, queries and alerts (see `local/reqs.md`).

This is the **skeleton**: the host shell, the design system, the `AkLogger.Sdk` contract and the extension registry. Its one built-in extension shows an
empty Logs page and a Settings page (light / dark / system theme). No log ingestion yet; the plan is `DevResources/plans/extensible-architecture-2026-11-25.md`.

## Shape

- `AkLogger.Sdk`: what extensions are written against (`IExtension`, `IExtensionHost`, the descriptor records, `LogRecord`, `Span`, `ILogSource`). It depends on nothing of the host.
- `AkLogger`: the Photino.Blazor desktop host (.NET 10, Blazor Hybrid in WebView2, Radzen). `AppState/Extensions.cs` is the registry; the side menu and the panes are
  driven by it (`SideNav`, `PaneHost`), and every address is routed through one catch-all page. Built-ins are `IExtension` classes listed in `Program.cs`.
- `AkLogger.Tests`: xunit + bUnit.

## Build, run, test

```
dotnet build AkLogger
dotnet run --project AkLogger
dotnet test AkLogger.Tests
```

Settings are kept in `%APPDATA%\AkLogger\settings.json` (`AK_SETTINGS` points the app at another file).

Checking the real app without touching your desktop (a hidden Windows desktop, driven over the Chrome DevTools Protocol):

```
. .\tests\hidden-app.ps1
$app = Start-HiddenApp -Port 9311
try { Eval "document.querySelector('nav.side').innerText"; Shot "start" } finally { Stop-HiddenApp $app }
```

## Acknowledgements

The design system was inspired by Pharrell Tan's work: https://dribbble.com/shots/16333822-Personal-Task-Management-App. Please keep this acknowledgement if you clone this project, unless you make your own theme.

The stack, architecture and design system are taken from the sibling project Repo Tasks (repo-task-manager). The font is Plus Jakarta Sans (SIL OFL, `AkLogger/wwwroot/fonts/OFL-LICENSE.txt`).
