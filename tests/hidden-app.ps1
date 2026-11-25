# Real-app verification without touching the user's desktop. Dot-source it:   . .\tests\hidden-app.ps1
#
#   $app = Start-HiddenApp -Port 9311 -ShotDir local\shots   # Debug build on a HIDDEN Windows desktop, CDP on that port; returns pid etc.
#   Eval "document.querySelector('nav.side').innerText"
#   Click (Center "document.querySelector('[aria-label=Settings]')");  Shot "settings"
#   Stop-HiddenApp $app                                       # always (in a finally): it unlocks the exe for the next build
#
# The window lives on a desktop made with CreateDesktop that is never shown: nothing appears on the user's screen, the app
# cannot take their foreground, and their keyboard never reaches it. Input goes over the Chrome DevTools Protocol
# (Input.dispatchMouseEvent / dispatchKeyEvent are real pointer and key events to the page), screenshots come from
# Page.captureScreenshot. (Copied from the task manager's tests/hidden-app.ps1, without its demo repository and board helpers.)
$ErrorActionPreference = 'Stop'
$script:Repo = Split-Path -Parent $PSScriptRoot
$script:Exe = Join-Path $script:Repo "AkLogger\bin\Debug\net10.0\AkLogger.exe"
$script:Out = Join-Path $script:Repo "tests\out"          # gitignored: scratch settings, WebView2 profile
$script:ShotDir = Join-Path $script:Out "shots"
$script:CdpPort = 9311

Add-Type -TypeDefinition @"
using System;using System.Runtime.InteropServices;
public static class HiddenDesk {
  [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)] struct STARTUPINFO { public int cb; public string r, desktop, title; public int x,y,xs,ys,xc,yc,fill,flags; public short show, cbr2; public IntPtr r2, i, o, e; }
  [StructLayout(LayoutKind.Sequential)] struct PROCESS_INFORMATION { public IntPtr hP, hT; public int pid, tid; }
  [DllImport("user32.dll", CharSet=CharSet.Unicode, SetLastError=true)] static extern IntPtr CreateDesktop(string n, string d, IntPtr dm, uint f, uint a, IntPtr sa);
  [DllImport("user32.dll")] static extern bool CloseDesktop(IntPtr h);
  [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)] static extern bool CreateProcess(string app, string cmd, IntPtr pa, IntPtr ta, bool inh, uint fl, IntPtr env, string dir, ref STARTUPINFO si, out PROCESS_INFORMATION pi);
  [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
  public static IntPtr Create(string name) { var h = CreateDesktop(name, null, IntPtr.Zero, 0, 0x10000000, IntPtr.Zero); if (h == IntPtr.Zero) throw new Exception("CreateDesktop failed, error " + Marshal.GetLastWin32Error()); return h; }
  public static void Close(IntPtr h) { CloseDesktop(h); }
  public static int Start(string exe, string args, string dir, string desktop) {
    var si = new STARTUPINFO(); si.cb = Marshal.SizeOf(typeof(STARTUPINFO)); si.desktop = desktop;
    PROCESS_INFORMATION pi;
    if (!CreateProcess(exe, "\"" + exe + "\" " + args, IntPtr.Zero, IntPtr.Zero, false, 0, IntPtr.Zero, dir, ref si, out pi)) throw new Exception("CreateProcess failed, error " + Marshal.GetLastWin32Error());
    CloseHandle(pi.hT); CloseHandle(pi.hP); return pi.pid;
  }
}
"@

# Starts the Debug build on a hidden desktop with its own WebView2 profile and settings file (a fresh one: the user's is never touched), waits for the
# debug port, connects CDP. -ShotDir: where Shot writes (default tests\out\shots).
function Start-HiddenApp([int]$Port = 9311, [string]$ShotDir) {
    $script:CdpPort = $Port   # agents working in parallel each take their own port
    if ($ShotDir) { $script:ShotDir = (New-Item -ItemType Directory -Force $ShotDir).FullName }
    if (-not (Test-Path $script:Exe)) { throw "not built: $script:Exe  (dotnet build AkLogger)" }
    $running = Get-Process AkLogger -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $script:Exe }
    if ($running) { throw "a Debug instance is already running (pid $($running[0].Id)); Stop-HiddenApp it first, or Stop-Process -Id $($running[0].Id)" }
    New-Item -ItemType Directory -Force $script:ShotDir | Out-Null
    $env:AK_SETTINGS = Join-Path $script:Out "settings-$PID.json"
    Remove-Item $env:AK_SETTINGS -ErrorAction SilentlyContinue
    $env:WEBVIEW2_USER_DATA_FOLDER = Join-Path $script:Out "webview"
    $env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS = "--remote-debugging-port=$script:CdpPort"
    $name = "ak-hidden-$PID"
    $desk = [HiddenDesk]::Create($name)
    $appPid = [HiddenDesk]::Start($script:Exe, "", (Split-Path $script:Exe), $name)
    $sw = [Diagnostics.Stopwatch]::StartNew()
    while ($true) {
        try { $null = Invoke-RestMethod "http://localhost:$script:CdpPort/json"; break } catch { }
        if ($sw.Elapsed.TotalSeconds -gt 40) { Stop-Process -Id $appPid -Force -ErrorAction SilentlyContinue; [HiddenDesk]::Close($desk); throw "debug port $script:CdpPort never opened (another WebView2 holding the profile? see Stop-HiddenApp)" }
        Start-Sleep -Milliseconds 300
    }
    Start-Sleep -Seconds 2   # Blazor boot + first render
    Connect-Cdp
    for ($i = 0; $i -lt 40 -and (Eval "!!document.querySelector('nav.side button')") -ne $true; $i++) { Start-Sleep -Milliseconds 250 }   # the first start with a new WebView2 profile is slower: wait for the side menu
    Start-Sleep -Seconds 1   # and for the window to settle at its size
    [pscustomobject]@{ Pid = $appPid; Desktop = $desk; DesktopName = $name; Settings = $env:AK_SETTINGS }
}

# Stops only what Start-HiddenApp started: the app, the WebView2 processes of its profile, the desktop.
function Stop-HiddenApp($App) {
    if ($script:ws) { try { $script:ws.Dispose() } catch { }; $script:ws = $null }
    if ($App -and (Get-Process -Id $App.Pid -ErrorAction SilentlyContinue)) { Stop-Process -Id $App.Pid -Force }
    $profile = Join-Path $script:Out "webview"
    Get-CimInstance Win32_Process -Filter "Name='msedgewebview2.exe'" | Where-Object { $_.CommandLine -like "*$profile*" } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
    Start-Sleep -Milliseconds 500
    if ($App) { [HiddenDesk]::Close($App.Desktop) }
}

# ── CDP ──
function Connect-Cdp {
    $target = (Invoke-RestMethod "http://localhost:$script:CdpPort/json")[0].webSocketDebuggerUrl
    $script:ws = New-Object System.Net.WebSockets.ClientWebSocket
    $script:ws.ConnectAsync([Uri]$target, [Threading.CancellationToken]::None).Wait()
    $script:cdpId = 0
}
function Send-Cdp($method, $params) {
    $script:cdpId++; $myId = $script:cdpId
    $bytes = [Text.Encoding]::UTF8.GetBytes((@{ id = $myId; method = $method; params = $params } | ConvertTo-Json -Depth 10 -Compress))
    $script:ws.SendAsync([ArraySegment[byte]]::new($bytes), [Net.WebSockets.WebSocketMessageType]::Text, $true, [Threading.CancellationToken]::None).Wait()
    while ($true) {   # replies interleave with events: read until ours
        $buf = New-Object byte[] 4194304; $all = ""
        try { $cts = New-Object Threading.CancellationTokenSource(8000); do { $res = $script:ws.ReceiveAsync([ArraySegment[byte]]::new($buf), $cts.Token).Result; $all += [Text.Encoding]::UTF8.GetString($buf, 0, $res.Count) } while (-not $res.EndOfMessage) }
        catch { throw "TIMEOUT waiting for $method (a runaway loop in app.js? check msedgewebview2 CPU)" }
        $obj = $all | ConvertFrom-Json
        if ($obj.id -eq $myId) { return $obj }
    }
}
# JavaScript in the page; returns the value, or "JS EXC: ...".
function Eval($expr) { $r = Send-Cdp "Runtime.evaluate" @{ expression = $expr; returnByValue = $true }; if ($r.result.exceptionDetails) { return "JS EXC: " + $r.result.exceptionDetails.exception.description }; return $r.result.result.value }
# Screenshot to <ShotDir>\<name>.png; returns the path.
function Shot($name) { $r = Send-Cdp "Page.captureScreenshot" @{ format = "png" }; $p = Join-Path $script:ShotDir "$name.png"; [IO.File]::WriteAllBytes($p, [Convert]::FromBase64String($r.result.data)); $p }
# A quick look at a theme without the Settings page (sets html[data-theme] only; the app's own switch is the Settings > Theme control).
function Set-Theme([ValidateSet("light", "dark")][string]$theme) { Eval "document.documentElement.dataset.theme = '$theme'; 'x'" | Out-Null; Start-Sleep -Milliseconds 300 }
# Real pointer / key events (CSS px, from getBoundingClientRect). $modifiers: 1 Alt, 2 Ctrl, 4 Meta, 8 Shift.
function Mouse($type, $x, $y, $buttons, $modifiers = 0) { if ($null -eq $x -or $null -eq $y) { return }; Send-Cdp "Input.dispatchMouseEvent" @{ type = $type; x = [int]$x; y = [int]$y; button = "left"; buttons = $buttons; clickCount = 1; modifiers = $modifiers } | Out-Null }
function Key($key, $code, $modifiers = 0) { Send-Cdp "Input.dispatchKeyEvent" @{ type = "keyDown"; key = $key; code = $key; windowsVirtualKeyCode = $code; modifiers = $modifiers } | Out-Null; Send-Cdp "Input.dispatchKeyEvent" @{ type = "keyUp"; key = $key; code = $key; windowsVirtualKeyCode = $code; modifiers = $modifiers } | Out-Null }
function Type-Text($text) { Send-Cdp "Input.insertText" @{ text = $text } | Out-Null }
# Centre of the element a JS expression returns, or $null (with a note) when there is none. Click takes these.
function Center($expr) { $v = Eval "(()=>{const el=($expr); if(!el) return null; const r=el.getBoundingClientRect(); return JSON.stringify({x:r.x+r.width/2,y:r.y+r.height/2})})()"; if ($null -eq $v -or "$v" -like "JS EXC*") { Write-Host "  [no element: $($expr.Substring(0, [Math]::Min(90, $expr.Length)))]"; return $null }; $v | ConvertFrom-Json }
function Click($p, $modifiers = 0) { if ($null -eq $p) { return }; Mouse "mousePressed" $p.x $p.y 1 $modifiers; Mouse "mouseReleased" $p.x $p.y 0 $modifiers; Start-Sleep -Milliseconds 300 }
