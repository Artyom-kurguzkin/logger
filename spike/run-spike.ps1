# Drives one spike run on a hidden desktop.
#   .\run-spike.ps1 -Exe <Host.exe> -Tag dev [-ExtArgs "--linger"] [-NoDispose] [-ExitBy button|kill]
# Output: out\<Tag>\result.log (written by the host + this script's CHECK lines), out\<Tag>\shot.png
param([Parameter(Mandatory)][string]$Exe, [string]$Tag = "dev", [string]$ExtArgs = "", [switch]$NoDispose, [ValidateSet("button", "kill")][string]$ExitBy = "button")
. "$PSScriptRoot\hidden-app.ps1"
$outDir = Join-Path $PSScriptRoot "out\$Tag"
if (Test-Path $outDir) { Remove-Item $outDir -Recurse -Force }
New-Item -ItemType Directory -Force $outDir | Out-Null
$log = Join-Path $outDir "result.log"
function Check($name, $ok, $detail = "") { $line = "{0} [CHECK] {1}: {2} {3}" -f (Get-Date -Format "HH:mm:ss.fff"), $name, $(if ($ok) { "PASS" } else { "FAIL" }), $detail; Add-Content $log $line; Write-Host $line; $ok }
function Wait-Until([scriptblock]$cond, [int]$seconds = 15) { $sw = [Diagnostics.Stopwatch]::StartNew(); while ($sw.Elapsed.TotalSeconds -lt $seconds) { if (& $cond) { return $true }; Start-Sleep -Milliseconds 300 }; $false }

$env:SPIKE_OUT = $outDir
$env:SPIKE_EXT_ARGS = $ExtArgs
if ($NoDispose) { $env:SPIKE_NO_DISPOSE = "1" } else { Remove-Item Env:SPIKE_NO_DISPOSE -ErrorAction SilentlyContinue }
$app = Start-HiddenApp $Exe
$page = $null; $frame = $null
try {
    $null = Wait-Until { (Get-Targets | Where-Object { $_.type -eq "page" }) }
    $pt = Get-Targets | Where-Object { $_.type -eq "page" } | Select-Object -First 1
    $page = New-Cdp $pt.webSocketDebuggerUrl
    Check "page target" ($null -ne $pt) $pt.url | Out-Null
    $null = Wait-Until { (Eval $page "document.getElementById('extframe') != null") -eq $true }
    Check "host page rendered iframe#extframe" ((Eval $page "document.getElementById('extframe') != null") -eq $true) | Out-Null
    Check "iframe sandbox attribute" ($true) ("sandbox=" + (Eval $page "document.getElementById('extframe').getAttribute('sandbox')") + " src=" + (Eval $page "document.getElementById('extframe').getAttribute('src')")) | Out-Null
    Check "host CSP meta" ($true) (Eval $page "document.querySelector('meta[http-equiv=Content-Security-Policy]').content") | Out-Null
    Start-Sleep -Seconds 2
    $targets = Get-Targets
    Add-Content $log ("CDP targets: " + (($targets | ForEach-Object { "$($_.type):$($_.url)" }) -join " | "))
    # the iframe is cross-origin (opaque origin): the page cannot reach into it, so it is its own CDP target when the browser puts it out of process
    $ft = $targets | Where-Object { $_.type -eq "iframe" -and $_.url -like "*/ext-ui/*" } | Select-Object -First 1
    if ($ft) { $frame = New-Cdp $ft.webSocketDebuggerUrl } else { Check "iframe is a CDP target" $false "no iframe target; page-only evaluation cannot reach a cross-origin frame" | Out-Null }
    if ($frame) {
        $ready = Wait-Until { (Eval $frame "document.getElementById('title').dataset.ready") -eq "1" }
        Check "(d) panel rendered and ui/initialize answered" $ready ("frame url=" + (Eval $frame "location.href") + " origin=" + (Eval $frame "String(location.origin)") + " theme='" + (Eval $frame "document.getElementById('theme').textContent") + "'") | Out-Null
        # is the sandbox / the response CSP really enforced? (negative checks)
        $ls = Eval $frame "(()=>{try{localStorage.getItem('x');return 'localStorage OK (origin NOT opaque)'}catch(e){return 'localStorage blocked: '+e.name}})()"
        Check "sandbox: opaque origin (storage blocked)" ($ls -like "localStorage blocked*") "$ls; window.origin=$(Eval $frame 'String(window.origin)') isSecureContext=$(Eval $frame 'String(isSecureContext)')" | Out-Null
        $viol = Eval $frame "new Promise(res=>{ const v=[]; document.addEventListener('securitypolicyviolation', e=>v.push(e.violatedDirective)); try{ new Function('return 1')(); v.push('eval RAN') }catch(e){ v.push('eval threw '+e.name) }; fetch(location.href).catch(()=>{}); new Image().src='/x.png'; setTimeout(()=>res(v.join(' | ')),700) })"
        Check "CSP enforced in frame (eval blocked, fetch->connect-src, img->img-src violations)" (($viol -like "*eval threw*") -and ($viol -like "*connect-src*") -and ($viol -like "*img-src*")) $viol | Out-Null
        $pj = Eval $page "(()=>{try{return 'parent reached: '+document.getElementById('extframe').contentWindow.document.title}catch(e){return 'parent cannot reach frame DOM: '+e.name}})()"
        Check "page cannot read the frame's DOM" ($pj -like "parent cannot*") $pj | Out-Null
        # real pointer click: iframe offset in the page + button rect in the frame
        $off = Eval $page "(()=>{const r=document.getElementById('extframe').getBoundingClientRect();return JSON.stringify({x:r.x,y:r.y})})()" | ConvertFrom-Json
        $btn = Eval $frame "(()=>{const r=document.getElementById('btn').getBoundingClientRect();return JSON.stringify({x:r.x+r.width/2,y:r.y+r.height/2})})()" | ConvertFrom-Json
        Click-At $page ($off.x + $btn.x) ($off.y + $btn.y)
        $got = Wait-Until { (Eval $frame "document.getElementById('result').textContent") -like "echo:*" } 8
        $res = Eval $frame "document.getElementById('result').textContent"
        Check "(e) click -> tools/call proxied -> echoed text in view" ($got -and $res -like "*hi from view*") "result='$res'" | Out-Null
        # host -> view notification
        Eval $page "spike.setTheme('dark'); 'x'" | Out-Null
        $dark = Wait-Until { (Eval $frame "document.getElementById('theme').textContent") -eq "dark" } 5
        Check "(e) host-context-changed reaches view" $dark ("theme='" + (Eval $frame "document.getElementById('theme').textContent") + "'") | Out-Null
        Shot $page (Join-Path $outDir "shot.png") | Out-Null
        Add-Content $log "screenshot: $(Join-Path $outDir 'shot.png')"
    }
    # (f) shutdown
    $extPid = (Select-String -Path $log -Pattern "ext pid=(\d+)" | Select-Object -Last 1).Matches.Groups[1].Value
    Add-Content $log "child pid from stderr: $extPid; host pid $($app.Pid)"
    if ($ExitBy -eq "button") {
        $b = Eval $page "(()=>{const r=document.getElementById('exit').getBoundingClientRect();return JSON.stringify({x:r.x+r.width/2,y:r.y+r.height/2})})()" | ConvertFrom-Json
        Click-At $page $b.x $b.y
    } else { Stop-Process -Id $app.Pid -Force }
    $hostGone = Wait-Until { -not (Get-Process -Id $app.Pid -ErrorAction SilentlyContinue) } 15
    Check "(f) host exited ($ExitBy)" $hostGone | Out-Null
    Start-Sleep -Seconds 4
    $alive = [bool](Get-Process -Id ([int]$extPid) -ErrorAction SilentlyContinue)
    Check "(f) child alive after host exit (ExtArgs='$ExtArgs', NoDispose=$NoDispose, ExitBy=$ExitBy)" $true "child alive = $alive"| Out-Null
    if ($alive) { Stop-Process -Id ([int]$extPid) -Force; Add-Content $log "(test cleanup: killed lingering child $extPid)" }
}
finally {
    Stop-HiddenApp $app
}
