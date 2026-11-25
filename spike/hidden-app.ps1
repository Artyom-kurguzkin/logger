# Adapted from repo-task-manager/tests/hidden-app.ps1 for the spike's Host.exe.  Dot-source it:  . .\hidden-app.ps1
# The app runs on a HIDDEN desktop (CreateDesktop, never shown); input and screenshots go over the Chrome DevTools Protocol, CDP port 9312.
# Differences from the original: no board helpers, and several CDP connections at once (the page and the cross-origin iframe are separate CDP targets).
$ErrorActionPreference = 'Stop'
$script:Out = Join-Path $PSScriptRoot "out"
$script:CdpPort = 9312

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

# Starts $Exe on a hidden desktop with its own WebView2 profile; waits for the debug port. Environment variables set before the call reach the app.
function Start-HiddenApp([Parameter(Mandatory)][string]$Exe, [string]$ExeArgs = "") {
    New-Item -ItemType Directory -Force $script:Out | Out-Null
    $env:WEBVIEW2_USER_DATA_FOLDER = Join-Path $script:Out "webview"
    $env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS = "--remote-debugging-port=$script:CdpPort"
    $name = "spike-hidden-$PID"
    $desk = [HiddenDesk]::Create($name)
    $appPid = [HiddenDesk]::Start($Exe, $ExeArgs, (Split-Path $Exe), $name)
    $sw = [Diagnostics.Stopwatch]::StartNew()
    while ($true) {
        try { $null = Invoke-RestMethod "http://localhost:$script:CdpPort/json"; break } catch { }
        if ($sw.Elapsed.TotalSeconds -gt 40) { Stop-Process -Id $appPid -Force -ErrorAction SilentlyContinue; [HiddenDesk]::Close($desk); throw "debug port $script:CdpPort never opened" }
        Start-Sleep -Milliseconds 300
    }
    [pscustomobject]@{ Pid = $appPid; Desktop = $desk }
}

function Stop-HiddenApp($App) {
    if ($App -and (Get-Process -Id $App.Pid -ErrorAction SilentlyContinue)) { Stop-Process -Id $App.Pid -Force }
    $profile = Join-Path $script:Out "webview"
    Get-CimInstance Win32_Process -Filter "Name='msedgewebview2.exe'" | Where-Object { $_.CommandLine -like "*$profile*" } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
    Start-Sleep -Milliseconds 500
    if ($App) { [HiddenDesk]::Close($App.Desktop) }
}

# ── CDP: one connection per target (page, iframe) ──
function Get-Targets { $r = Invoke-RestMethod "http://localhost:$script:CdpPort/json"; foreach ($x in $r) { $x } }   # Invoke-RestMethod hands the JSON array over as ONE object
function New-Cdp($wsUrl) {
    $ws = New-Object System.Net.WebSockets.ClientWebSocket
    $ws.ConnectAsync([Uri]$wsUrl, [Threading.CancellationToken]::None).Wait()
    [pscustomobject]@{ Ws = $ws; Id = 0 }
}
function Send-Cdp($c, $method, $params = @{}) {
    $c.Id++; $myId = $c.Id
    $bytes = [Text.Encoding]::UTF8.GetBytes((@{ id = $myId; method = $method; params = $params } | ConvertTo-Json -Depth 10 -Compress))
    $c.Ws.SendAsync([ArraySegment[byte]]::new($bytes), [Net.WebSockets.WebSocketMessageType]::Text, $true, [Threading.CancellationToken]::None).Wait()
    while ($true) {
        $buf = New-Object byte[] 4194304; $all = ""
        try { $cts = New-Object Threading.CancellationTokenSource(8000); do { $res = $c.Ws.ReceiveAsync([ArraySegment[byte]]::new($buf), $cts.Token).Result; $all += [Text.Encoding]::UTF8.GetString($buf, 0, $res.Count) } while (-not $res.EndOfMessage) }
        catch { throw "TIMEOUT waiting for $method" }
        $obj = $all | ConvertFrom-Json
        if ($obj.id -eq $myId) { return $obj }
    }
}
function Eval($c, $expr) { $r = Send-Cdp $c "Runtime.evaluate" @{ expression = $expr; returnByValue = $true; awaitPromise = $true; allowUnsafeEvalBlockedByCSP = $false }; if ($r.result.exceptionDetails) { return "JS EXC: " + $r.result.exceptionDetails.exception.description }; return $r.result.result.value }
function Shot($c, $path) { $r = Send-Cdp $c "Page.captureScreenshot" @{ format = "png" }; [IO.File]::WriteAllBytes($path, [Convert]::FromBase64String($r.result.data)); $path }
function Mouse($c, $type, $x, $y, $buttons) { Send-Cdp $c "Input.dispatchMouseEvent" @{ type = $type; x = [int]$x; y = [int]$y; button = "left"; buttons = $buttons; clickCount = 1 } | Out-Null }
function Click-At($c, $x, $y) { Mouse $c "mousePressed" $x $y 1; Mouse $c "mouseReleased" $x $y 0; Start-Sleep -Milliseconds 300 }
