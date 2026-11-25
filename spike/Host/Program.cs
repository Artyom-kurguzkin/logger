using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Photino.Blazor;
using SpikeHost;

internal static class Program
{
    [STAThread]   // Photino needs an STA main thread (async Main / top-level await would be MTA: the window never appears)
    private static void Main(string[] args)
    {
        SpikeState S = new();
        try { S.StartAsync().GetAwaiter().GetResult(); }
        catch (Exception e) { S.Log("FATAL", e.ToString()); }

        var builder = PhotinoBlazorAppBuilder.CreateDefault(args);
        builder.Services.AddSingleton(S);
        builder.RootComponents.Add<App>("#app");
        var app = builder.Build();
        app.MainWindow.SetTitle("spike host").SetUseOsDefaultSize(false).SetSize(900, 700).SetLogVerbosity(0);
        S.Exit = () => app.MainWindow.Invoke(() => app.MainWindow.Close());
        app.Run();

        S.Log("f", "window closed; shutting down");
        if (Environment.GetEnvironmentVariable("SPIKE_NO_DISPOSE") != "1") S.StopAsync().GetAwaiter().GetResult();
        else S.Log("f", "SPIKE_NO_DISPOSE=1: not disposing the MCP client; process just ends");
    }
}
namespace SpikeHost
{
    public sealed class SpikeState
    {
        readonly string outDir = Environment.GetEnvironmentVariable("SPIKE_OUT") ?? Path.Combine(AppContext.BaseDirectory, "out");
        readonly object gate = new();
        McpClient? client; WebApplication? web; string panelHtml = "";
        public string PanelUrl { get; private set; } = "about:blank";
        public string Summary { get; private set; } = "starting";
        public Action? Exit;
        public void RequestExit() => Exit?.Invoke();

        public void Log(string step, string text)
        {
            lock (gate) { Directory.CreateDirectory(outDir); File.AppendAllText(Path.Combine(outDir, "result.log"), $"{DateTime.Now:HH:mm:ss.fff} [{step}] {text}{Environment.NewLine}"); }
        }

        string ExtExe()
        {
            string[] candidates = [Path.Combine(AppContext.BaseDirectory, "ext", "Ext.exe"), Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Ext", "bin", "Debug", "net10.0", "Ext.exe"))];
            return candidates.FirstOrDefault(File.Exists) ?? throw new FileNotFoundException("Ext.exe not found", string.Join(" | ", candidates));
        }

        public async Task StartAsync()
        {
            Log("start", $"host pid={Environment.ProcessId} base={AppContext.BaseDirectory}");
            string exe = ExtExe();
            Log("start", "launching " + exe);
            StdioClientTransport transport = new(new StdioClientTransportOptions
            {
                Command = exe,
                Arguments = (Environment.GetEnvironmentVariable("SPIKE_EXT_ARGS") ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries),
                Name = "spike-ext",
                StandardErrorLines = line => Log("ext-stderr", line),
                ShutdownTimeout = TimeSpan.FromSeconds(3),
            });
            client = await McpClient.CreateAsync(transport, new McpClientOptions { ClientInfo = new Implementation { Name = "spike-host", Version = "0" } });

            // (a) tools
            IList<McpClientTool> tools = await client.ListToolsAsync();
            Log("a", "tools: " + string.Join(", ", tools.Select(t => t.Name)));
            CallToolResult echo = await client.CallToolAsync("echo", new Dictionary<string, object?> { ["text"] = "from host" });
            Log("a", "echo -> " + string.Join("", echo.Content.OfType<TextContentBlock>().Select(c => c.Text)));

            // (b) capabilities / protocol
            Log("b", "negotiated protocol version: " + client.NegotiatedProtocolVersion);
            Log("b", "server info: " + client.ServerInfo?.Name + " " + client.ServerInfo?.Version);
            IDictionary<string, object>? ext = client.ServerCapabilities.Extensions;
            Log("b", "ServerCapabilities.Extensions keys: " + (ext is null ? "(null)" : string.Join(", ", ext.Keys)));
            Log("b", "dev.aklogger/logs = " + (ext is not null && ext.TryGetValue("dev.aklogger/logs", out object? v) ? JsonSerializer.Serialize(v) : "(missing)"));
            Log("b", "full ServerCapabilities: " + JsonSerializer.Serialize(client.ServerCapabilities, McpJsonUtilities.DefaultOptions));
            try
            {   // the raw server/discover answer too
                JsonRpcResponse d = await client.SendRequestAsync(new JsonRpcRequest { Method = "server/discover", Params = new JsonObject() });
                Log("b", "raw server/discover -> " + d.Result?.ToJsonString());
            }
            catch (Exception e) { Log("b", "raw server/discover failed: " + e.Message); }

            // (c) custom method
            JsonRpcResponse parsed = await client.SendRequestAsync(new JsonRpcRequest
            {
                Method = "aklogger/parser/parse",
                Params = new JsonObject { ["parserId"] = "p1", ["lines"] = new JsonArray("line one", "line two") },
            });
            Log("c", "aklogger/parser/parse -> " + parsed.Result?.ToJsonString());

            try { await client.SendRequestAsync(new JsonRpcRequest { Method = "aklogger/does-not-exist", Params = new JsonObject() }); Log("c", "unknown custom method: unexpectedly succeeded"); }
            catch (Exception e) { Log("c", "unknown custom method -> " + e.GetType().Name + ": " + e.Message + (e is McpProtocolException pe ? " (code " + (int)pe.ErrorCode + ")" : "")); }

            // (d) the ui:// resource, served from in-process Kestrel with a restrictive CSP
            ReadResourceResult res = await client.ReadResourceAsync("ui://hello/panel");
            TextResourceContents text = res.Contents.OfType<TextResourceContents>().First();
            panelHtml = text.Text;
            Log("d", $"resources/read ui://hello/panel mime={text.MimeType} bytes={panelHtml.Length}");
            WebApplicationBuilder wb = WebApplication.CreateSlimBuilder();
            wb.WebHost.UseUrls("http://127.0.0.1:0");
            wb.Logging.ClearProviders();
            web = wb.Build();
            web.MapGet("/ext-ui/{ext}/{panel}", (HttpContext ctx) =>
            {
                ctx.Response.Headers.ContentSecurityPolicy = "default-src 'none'; script-src 'unsafe-inline'; style-src 'unsafe-inline'";
                Log("d", "kestrel served " + ctx.Request.Path);
                return Results.Text(panelHtml, "text/html; charset=utf-8");
            });
            await web.StartAsync();
            int port = new Uri(web.Urls.First()).Port;
            PanelUrl = $"http://127.0.0.1:{port}/ext-ui/hello/panel";
            Log("d", "panel url " + PanelUrl);

            // the host page's CSP names this port: write wwwroot/index.html from the template before the window loads it
            string www = Path.Combine(AppContext.BaseDirectory, "wwwroot");
            File.WriteAllText(Path.Combine(www, "index.html"), File.ReadAllText(Path.Combine(www, "index.template.html")).Replace("__PORT__", port.ToString()));
            Summary = $"extension up; negotiated {client.NegotiatedProtocolVersion}; panel {PanelUrl}";
        }

        // (e) the view's tools/call, proxied to the extension
        public async Task<string> CallToolAsync(string name, string argsJson)
        {
            Log("e", $"host proxying tools/call {name} {argsJson}");
            Dictionary<string, object?> args = JsonSerializer.Deserialize<Dictionary<string, object?>>(argsJson) ?? [];
            CallToolResult r = await client!.CallToolAsync(name, args);
            string json = JsonSerializer.Serialize(r, McpJsonUtilities.DefaultOptions);
            Log("e", "extension answered " + json);
            return json;
        }

        public async Task StopAsync()
        {
            try { if (web is not null) await web.StopAsync(); } catch { }
            if (client is not null) { Stopwatch sw = Stopwatch.StartNew(); await client.DisposeAsync(); Log("f", $"client disposed in {sw.ElapsedMilliseconds} ms"); }
            Log("f", "host done");
        }
    }
}
