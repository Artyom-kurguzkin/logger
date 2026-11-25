# ak-logger: task-manager architecture, adapted for extensions

Status (2026-11-25): steps 1 (spike) and 2 (skeleton) done, in review; steps 3-5 planned (#wi3, #wi4, #wi5).

## Context

ak-logger is a local log explorer (logs, OpenTelemetry traces, graphs, queries, alerts per `local/reqs.md`). It takes its stack, architecture and design system from `../repo-task-manager`, and must support third-party add-ons that can add sources/parsers, views, queries/alerts, agent tools, and whole unrelated panels (e.g. a control panel for an external app).

Decisions reached in discussion: built-in features run in-process; third-party extensions run as child processes behind a language-neutral protocol with iframe UI. Research (below) shows that protocol already exists: **an extension is an MCP server; its UI is an MCP App.** We add only a small vendor extension for the log-specific parts.

## What the research found

| System | What it does | What we take |
|---|---|---|
| **MCP 2026-07-28** (current; C# SDK 2.4.0 implements it, the version the task manager already uses) | JSON-RPC, newline-delimited over stdio; stderr is free-form logging; no `initialize` any more: `server/discover` + per-request `_meta` version/capabilities; `extensions` capability keyed `{vendor}/{name}` with a `revision`; servers cannot send requests on stdio; `notifications/cancelled`; shutdown = close stdin, wait, kill (Job Objects on Windows); client should restart on unexpected exit | The whole base protocol, unchanged. Host always calls, extension answers (pull). |
| **MCP Apps** (`io.modelcontextprotocol/ui`, stable 2026-01-26) | UI is an HTML document at a `ui://` resource, rendered in a sandboxed iframe on a different origin; view talks JSON-RPC over `postMessage` (`ui/initialize`, `tools/call` proxied by host, `ui/notifications/host-context-changed`, `size-changed`, `ui/open-link`, `ui/resource-teardown`); host passes theme + 76 standard CSS variables; CSP built from declared `connectDomains`/`resourceDomains`, default deny | The whole UI contract. Gaps we fill: views not tied to a tool call, and external-URL content (both absent/deferred in the spec). |
| **LSP 3.17/3.18** | Capabilities exchanged up front; cancellation and progress by token | Same ideas already in MCP; nothing extra. |
| **go-plugin / Grafana** | Subprocess per plugin, one handshake line, protocol major version, additive changes are non-breaking, "plugins can't crash the host", health check call, authors use an SDK not the raw protocol | Additive-only evolution, health check, ship an SDK helper. |
| **VS Code** | Declarative contribution points in the manifest so UI appears without starting the extension; activation events start it lazily; webviews: CSP default-src none, postMessage only, `getState/setState`, theme via CSS variables | Manifest lists contributions; process starts on first use. |

Sources: [MCP changelog 2026-07-28](https://modelcontextprotocol.io/specification/2026-07-28/changelog), [stdio transport](https://modelcontextprotocol.io/specification/2026-07-28/basic/transports/stdio.md), [extensions overview](https://modelcontextprotocol.io/docs/extensions/overview.md), [MCP Apps spec](https://raw.githubusercontent.com/modelcontextprotocol/ext-apps/main/specification/2026-01-26/apps.mdx), [C# SDK releases](https://github.com/modelcontextprotocol/csharp-sdk/releases), [go-plugin internals](https://github.com/hashicorp/go-plugin/blob/main/docs/internals.md), [Grafana plugin protocol](https://grafana.com/developers/plugin-tools/key-concepts/backend-plugins/plugin-protocol), [VS Code webviews](https://code.visualstudio.com/api/extension-guides/webview), [contribution points](https://code.visualstudio.com/api/references/contribution-points), [LSP 3.17](https://microsoft.github.io/language-server-protocol/specifications/lsp/3.17/specification/).

## What carries over unchanged from the task manager

- **Stack**: .NET 10, Photino.Blazor 4 (Blazor Hybrid in WebView2), Radzen.Blazor, ModelContextProtocol 2.4.0 on in-process Kestrel, xunit + bUnit, Nerdbank.GitVersioning, `dist` single-file publish. No npm.
- **Process model** (`TaskManager/Program.cs`): one exe; desktop mode + `--mcp` stdio shim (`Mcp/McpShim.cs`) relaying to loopback Kestrel with bearer token (`Api/AgentEndpoint.cs`).
- **Modular monolith**: leaf modules exposing static `{Module}Api` + `PublicDTOs`, all UI in the host, host folders `AppState/ Helpers/ BackgroundJobs/ Components/`, `ModuleBoundaryTests`.
- **UI plumbing**: `RtmComponent`, `RepoPage` pattern, `Panes` + `PaneHost` (`DynamicComponent`), `UiService` + `Overlays`, `AppErrorBoundary`.
- **Design system**: `wwwroot/css/app.css` layers `tokens, base, primitives, layout, nav, overlays, list` + the Worklog timeline block, `wwwroot/fonts/`, `js/theme.js`, theme/`place`/scroll-fade parts of `js/app.js`, `Ui.Icon`/`Palette`/`Pick`, `Components/Shared/{Seg,Menu,MenuItem,Dropdown,BackButton}.razor`. Keep the README design acknowledgement.

Dropped: `OpenSpecStore`, `WorkItems`, `Notes`, `Features`, `GitHistory`, `LinkedTestRuns`, `BoardSync`, all board pages.

## What changes

### 1. Registry replaces the closed switches (in-process door)

`AkLogger/AppState/Extensions.cs` holds descriptor lists; built-ins fill it through:

```csharp
public interface IExtension { void Register(IExtensionHost host); }
public interface IExtensionHost {
    void AddSource(SourceProvider p);      // Id, Name, Fields, Func<settings, ILogSource>
    void AddParser(ParserProvider p);      // Id, Name, TryParse(line) -> LogRecord
    void AddView(ViewDescriptor v);        // Id, Title, Icon, Route, Component type OR ui:// resource
    void AddQueryFunction(QueryFunction f);
    void AddAlertAction(AlertAction a);
    void AddTools(Type mcpToolType);
}
```

Descriptor shape copied from `StorageProvider` (`AppState/BoardStorage.cs:16-22`). These live in `AkLogger.Sdk` with `LogRecord`/`Span`.

| Task manager (closed) | ak-logger |
|---|---|
| `PaneHost.razor:26-34` path `switch` | `Extensions.Views` by route |
| `SideNav.razor:54-57` fixed items | loop over views with nav placement |
| `Ui.Paths` private icons | icon on the descriptor |
| `SettingsPage.razor` static sections | form rendered from `Fields` |
| `.WithTools<BoardTools>()` | loop over `Extensions.Tools` + proxied extension tools |

Log list, traces, graphs, alerts, file source, OTLP receiver are `IExtension` classes in referenced projects.

### 2. Third-party extension = MCP server + MCP App

- **Install**: folder `%LOCALAPPDATA%\AkLogger\extensions\<id>\` with `extension.json`: `id, name, version, author, command, args, permissions`, and `contributes` (views `{id,title,icon,resourceUri,nav}`, sources, parsers, alert actions, each with `fields`). Contributions are declarative, so nav entries and settings forms show without starting the process; it starts on first use (VS Code model).
- **Logic channel**: host launches `command`, speaks MCP 2026-07-28 over stdio using the C# SDK client. Tools are proxied to agents as `<extId>_<tool>`; any existing MCP server is already a valid tools-only extension.
- **Log-specific calls**: vendor extension `dev.aklogger/logs` (placeholder prefix; needs a domain the project owns), `revision: "1"`, advertised in `server/discover`. Methods, all host→extension, additive-only evolution:
  - `aklogger/source/read {sourceId, settings, cursor}` → `{records[], cursor, done}` (polled; servers cannot push on stdio)
  - `aklogger/parser/parse {parserId, lines[]}` → `{records[]}` (batched)
  - `aklogger/alert/fire {actionId, settings, alert}`
  - `aklogger/health` → `{ok, message}`
- **UI**: MCP Apps as specified. View HTML comes from `resources/read` of a `ui://` resource; host serves it from Kestrel at a loopback origin distinct from the Photino page (`http://127.0.0.1:<port>/ext-ui/<id>/...`) with the CSP header built from `_meta.ui.csp`, in `<iframe sandbox="allow-scripts">`. `ExtensionFrame.razor` + a few lines in `app.js` implement the host side of `ui/initialize`, `host-context-changed`, `size-changed`, `ui/open-link`, proxied `tools/call`/`resources/read`, `ui/resource-teardown`. Host CSP gains `frame-src` for that origin only.
- **Our two additions to MCP Apps**: (a) a view can be standalone (listed in `contributes.views`, no tool call; `tool-input` is simply never sent); (b) host context gains `styles.variables` filled from `app.css` tokens mapped onto the 76 standard names, so any MCP App looks native.
- **Changed from the earlier discussion**: Blazor Server panels are out of v1. MCP Apps only supports inline HTML (external URLs are deferred in the spec), and following the standard is worth more than that path. A .NET author writes logic with `AkLogger.Sdk` (thin helper over the C# MCP server SDK) and UI as an HTML page.
- **Limits**: an extension owns whole panes; it cannot inject into host pages.

### 3. Trust and isolation

- Found extensions are disabled until enabled in Settings › Extensions (shows manifest, permissions, declared CSP domains). `--no-extensions` flag.
- Process supervisor: Windows Job Object so children die with the host; request timeouts + `notifications/cancelled`; on unexpected exit restart with backoff, after 3 failures disable and toast. Shutdown: close stdin, wait, kill.
- View-initiated calls go only to that extension's own tools, and are logged.
- A child process still has the user's rights. `permissions` is in the manifest from day one; enforcing it with AppContainer is a later step. Not built: signing, marketplace.

### 4. Things the task manager lacks

- **Store**: leaf module `LogStore` on `Microsoft.Data.Sqlite` + FTS5 (`LogStoreApi.Append/Query/Changed`). Queries are SQL.
- **Virtualization**: log list uses Blazor `Virtualize<T>` over `LogStoreApi.Query`.
- **OTLP**: `POST /v1/logs`, `/v1/traces` on the existing Kestrel.
- **Alerts**: saved query + threshold on a timer in `BackgroundJobs/`.

## Build order

1. **Spike (throwaway, de-risks everything else)**: minimal Photino.Blazor host + one stdio MCP extension. Confirms: C# SDK client can launch a stdio server and send a custom `aklogger/*` request; `server/discover` shows our extension key; a `ui://` HTML resource renders in a sandboxed iframe from the loopback origin under Photino's CSP; `postMessage` round trip incl. a proxied `tools/call`. If the SDK cannot send custom methods, fall back to expressing the log calls as tools with a reserved `_aklogger_` prefix hidden from agents.
2. **Skeleton**: `AkLogger.slnx`, host shell + design system copied, `AkLogger.Sdk`, `Extensions` registry, registry-driven `PaneHost`/`SideNav`.
3. **First built-in end to end**: `LogStore`, file-tail source, JSON-lines parser, virtualized log list.
4. **External extensions**: manifest, supervisor, `RemoteExtension : IExtension` adapter, `ExtensionFrame`, Settings › Extensions, safe mode, `docs/extension-protocol.md`, samples `samples/HelloDotnet` (SDK) and `samples/hello-script` (plain Node or Python MCP server + HTML).
5. **Remaining built-ins**: OTLP receiver, trace explorer, queries, graphs, alerts.

Execution follows the repo's delegate-workflow: this plan copied to `DevResources/plans/extensible-architecture-2026-11-25.md`, one board work item per step, tests first, result left uncommitted. This session does steps 1-2 (spike result decides the rest); steps 3-5 become dependent work items.

## Verification

- `dotnet test`: `ModuleBoundaryTests`; registry tests; supervisor test that starts the sample extension from a temp folder, asserts its parser, view and tool are registered, that killing the process restarts then disables it without crashing the host, and that an unknown `revision` is refused.
- Real app (copy of `tests/hidden-app.ps1`, CDP on a hidden desktop): enable both samples, see nav entries and panes in light and dark (theme follows via `host-context-changed`); call a proxied tool through `--mcp`; `--no-extensions` shows none.
- Repeat on the published single-file exe.

## Spike findings (2026-11-25, step 1)

Every point worked, in a debug build and from the published single-file exe (`spike/out/*/result.log`). The `_aklogger_` tool fallback is not needed.

- **Protocol**: the C# SDK client probes `server/discover` and negotiates `2026-07-28`; our key shows up as `ServerCapabilities.Extensions["dev.aklogger/logs"] = {"revision":"1"}`. The value must be a `JsonElement`/`JsonNode`; a nested dictionary breaks discovery.
- **Custom methods**: client `McpClient.SendRequestAsync(new JsonRpcRequest { Method, Params })`; server `McpServerOptions.RequestHandlers` (`McpServerRequestHandler`). The server side is experimental (`MCPEXP002`), so it may change between SDK versions. An unknown method returns `-32601`, usable for feature detection.
- **Launching**: `StdioClientTransport` + `McpClient.CreateAsync`; stderr lines arrive through `StandardErrorLines`.
- **UI**: `<iframe sandbox="allow-scripts">` on `http://127.0.0.1:<port>/ext-ui/...` served by Kestrel with its own CSP header. Inside the frame the origin is opaque, storage is blocked, the CSP is enforced, and the host page cannot read the frame. The host page CSP needs `frame-src http://127.0.0.1:<port>`; with `'none'` the frame is blocked. Replies to the frame must use target origin `'*'`, checked by `e.source`.
- **Open design point for step 4**: the port is known only after Kestrel binds, and the host CSP is a meta tag in `index.html`. The spike rewrote `index.html` at startup, which needs a writable folder beside the exe. Use a fixed port (as the task manager does with 7431) or serve `index.html` from a handler.
- **Message race**: the frame can post `ui/initialize` before Blazor's first render; the host script must listen from page load and queue.
- **Shutdown**: the SDK closes stdin, waits `ShutdownTimeout`, then kills. If the host is killed, a child that ignores stdin EOF survives, so the Job Object is required.
- **Photino**: needs `[STAThread] static void Main`; with an async top-level main the window never appears.
- **Size**: a self-contained single-file .NET extension is about 74 MB; extensions should be framework-dependent or use another runtime.
