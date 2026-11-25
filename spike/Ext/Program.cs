using System.ComponentModel;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
Console.Error.WriteLine($"ext pid={Environment.ProcessId} started");
builder.Logging.SetMinimumLevel(LogLevel.Warning);
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);   // stdout is the protocol; logs go to stderr

builder.Services.AddMcpServer(o =>
{
    o.ServerInfo = new Implementation { Name = "spike-ext", Version = "0.0.1" };
    o.Capabilities = new ServerCapabilities
    {
        // the vendor extension, keyed {vendor}/{name} with a revision
        Extensions = new Dictionary<string, object> { ["dev.aklogger/logs"] = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>("{\"revision\":\"1\"}") },
    };
    // the CUSTOM, non-standard request: aklogger/parser/parse {parserId, lines[]} -> {records:[{message}]}
    o.RequestHandlers ??= new List<McpServerRequestHandler>();
    o.RequestHandlers.Add(new McpServerRequestHandler
    {
        Method = "aklogger/parser/parse",
        Handler = (ctx, ct) =>
        {
            var lines = ctx.Params?["lines"]?.AsArray() ?? [];
            var records = new JsonArray();
            foreach (var l in lines) records.Add(new JsonObject { ["message"] = l?.GetValue<string>(), ["parserId"] = ctx.Params?["parserId"]?.GetValue<string>() });
            return ValueTask.FromResult<JsonNode?>(new JsonObject { ["records"] = records, ["seenParams"] = ctx.JsonRpcRequest.Params?.ToJsonString() });   // seenParams: what the wire carried (shows the SDK's per-request _meta)
        },
    });
})
.WithStdioServerTransport()
.WithTools<Tools>()
.WithResources<Panels>();

await builder.Build().RunAsync();
if (args.Contains("--linger")) await Task.Delay(-1);   // test only: a child that ignores stdin EOF

[McpServerToolType]
public sealed class Tools
{
    [McpServerTool(Name = "echo"), Description("Echoes the text back.")]
    public static string Echo([Description("text to echo")] string text) => "echo: " + text;
}

[McpServerResourceType]
public sealed class Panels
{
    [McpServerResource(UriTemplate = "ui://hello/panel", Name = "hello-panel", MimeType = "text/html;profile=mcp-app")]
    [Description("The hello MCP App.")]
    public static string Panel() => Html;

    const string Html = """
<!doctype html><html><head><meta charset="utf-8"><title>hello</title>
<style>body{font-family:sans-serif;margin:8px} #theme{font-weight:bold}</style></head>
<body>
<h3 id="title">hello panel</h3>
<div>theme: <span id="theme">?</span></div>
<button id="btn">call echo</button>
<div>result: <span id="result"></span></div>
<script>
let nextId = 1; const pending = new Map();
function rpc(method, params) { const id = nextId++; return new Promise((res, rej) => { pending.set(id, { res, rej }); window.parent.postMessage({ jsonrpc: "2.0", id, method, params }, "*"); }); }
function notify(method, params) { window.parent.postMessage({ jsonrpc: "2.0", method, params }, "*"); }
window.addEventListener("message", e => {
  const m = e.data; if (!m || m.jsonrpc !== "2.0") return;
  if (m.id !== undefined && !m.method && pending.has(m.id)) { const p = pending.get(m.id); pending.delete(m.id); m.error ? p.rej(m.error) : p.res(m.result); return; }
  if (m.method === "ui/notifications/host-context-changed") document.getElementById("theme").textContent = m.params?.theme ?? "?";
});
(async () => {
  const init = await rpc("ui/initialize", { protocolVersion: "2026-01-26", appInfo: { name: "hello", version: "1" }, appCapabilities: {} });
  document.getElementById("theme").textContent = init.hostContext?.theme ?? "none";
  notify("ui/notifications/initialized", {});
  document.title = "ready";
  document.getElementById("title").dataset.ready = "1";
})().catch(err => { document.getElementById("theme").textContent = "init failed: " + JSON.stringify(err); });
document.getElementById("btn").onclick = async () => {
  try { const r = await rpc("tools/call", { name: "echo", arguments: { text: "hi from view" } }); document.getElementById("result").textContent = r.content?.[0]?.text ?? JSON.stringify(r); }
  catch (err) { document.getElementById("result").textContent = "error: " + JSON.stringify(err); }
};
</script></body></html>
""";
}
