param([Parameter(Mandatory=$true)][string]$ContextPath,[hashtable]$TestParameters=@{})

$ErrorActionPreference = 'Stop'
$context = Get-Content -LiteralPath $ContextPath -Raw | ConvertFrom-Json
if (-not $context.SessionUrl) { throw 'Startup requires the runner-owned Managed Appium session.' }
if (-not $context.Serial) { throw 'Startup requires the reserved device serial in runner context.' }
$script:sessionUrl = [string]$context.SessionUrl
$script:serial = [string]$context.Serial
$script:outputDirectory = [string]$context.OutputDirectory
$script:package = 'com.doingit.app'
$iterations = 20
if ($TestParameters.ContainsKey('Iterations')) { $iterations = [int]$TestParameters.Iterations }
if ($iterations -lt 1 -or $iterations -gt 50) { throw 'Iterations must be between 1 and 50.' }
$script:processIds = [System.Collections.Generic.List[string]]::new()

function Invoke-Driver([string]$Method,[string]$Path,$Body=$null) {
    $request = @{ Uri = "$script:sessionUrl$Path"; Method = $Method; TimeoutSec = 60 }
    if ($null -ne $Body) { $request.ContentType = 'application/json; charset=utf-8'; $request.Body = ConvertTo-Json -InputObject $Body -Depth 15 -Compress }
    Invoke-RestMethod @request
}
function Invoke-Adb([string[]]$Arguments) {
    $result = & adb -s $script:serial @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) { throw "adb -s $script:serial $($Arguments -join ' ') failed ($LASTEXITCODE): $result" }
    [string]::Join("`n", @($result | ForEach-Object { [string]$_ }))
}
function Get-AppProcessId {
    $result = & adb -s $script:serial shell pidof $script:package 2>&1
    if ($LASTEXITCODE -ne 0) { return '' }
    (([string]::Join(' ',@($result | ForEach-Object { [string]$_ }))).Trim() -split '\s+')[0]
}
function Terminate-App { Invoke-Driver 'POST' '/appium/device/terminate_app' @{ appId=$script:package } | Out-Null }
function Activate-App { Invoke-Driver 'POST' '/appium/device/activate_app' @{ appId=$script:package } | Out-Null }
function Press-Home { Invoke-Driver 'POST' '/appium/device/press_keycode' @{ keycode=3 } | Out-Null }
function Find-XPath([string]$XPath) {
    $element = (Invoke-Driver 'POST' '/element' @{ using='xpath'; value=$XPath }).value
    $id = $element.'element-6066-11e4-a52e-4f735466cecf'; if (-not $id) { $id = $element.ELEMENT }
    if (-not $id) { throw "No element identifier returned for $XPath" }; [string]$id
}
function Get-Source { [string](Invoke-Driver 'GET' '/source').value }
function Assert-NoFailureOverlay([string]$Source,[int]$Cycle) {
    foreach ($marker in @('We couldn''t open the saved planner','Couldn''t change page','Couldn''t open the shortcut')) {
        if ($Source.Contains($marker)) { throw "Startup failure overlay '$marker' appeared during cycle $Cycle." }
    }
}
function Wait-ForHome([int]$Cycle) {
    for ($attempt=0; $attempt -lt 60; $attempt++) {
        $source = Get-Source
        Assert-NoFailureOverlay $source $Cycle
        if ($source.Contains('TabHome')) { return $source }
        Start-Sleep -Milliseconds 500
    }
    throw "Home did not appear after launch/resume cycle $Cycle."
}
function Wait-ForStopped([int]$Cycle) {
    for ($attempt=0; $attempt -lt 20; $attempt++) {
        if (-not (Get-AppProcessId)) { return }
        Start-Sleep -Milliseconds 250
    }
    throw "App process remained alive after Appium terminateApp in cycle $Cycle."
}
function Save-UnfilteredLogcat([string]$Name) {
    $log = Invoke-Adb @('logcat','-d','-v','epoch','-T',"$script:deviceStartEpoch.000")
    [IO.File]::WriteAllText((Join-Path $script:outputDirectory $Name),$log,[Text.UTF8Encoding]::new($false))
    $log
}
function Count-AppFatal([string]$Log) {
    $lines = @($Log -split '\r?\n')
    $managed = 0; $native = 0
    for ($i=0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match 'FATAL EXCEPTION:') {
            $end = [Math]::Min($i + 4,$lines.Count - 1)
            if (($lines[$i..$end] -join "`n") -match 'Process:\s*com\.doingit\.app,\s*PID:\s*\d+') { $managed++ }
        }
        if ($lines[$i] -match 'Fatal signal') {
            $end = [Math]::Min($i + 35,$lines.Count - 1)
            $block = $lines[$i..$end] -join "`n"
            $owned = $block.Contains($script:package)
            foreach ($appProcessId in $script:processIds) {
                if ($block -match "\bpid[:= ]+$([regex]::Escape($appProcessId))\b") { $owned = $true; break }
            }
            if ($owned) { $native++ }
        }
    }
    @{ Managed=$managed; Native=$native }
}

if (-not (Test-Path -LiteralPath $script:outputDirectory)) { throw 'Runner OutputDirectory is missing.' }
$null = Invoke-Driver 'POST' '/timeouts' @{ implicit=500 }
$script:deviceStartEpoch = (Invoke-Adb @('shell','date','+%s')).Trim()
if ($script:deviceStartEpoch -notmatch '^\d+$') { throw "Could not read device epoch time: $script:deviceStartEpoch" }
$startPid = Get-AppProcessId
[IO.File]::WriteAllText((Join-Path $script:outputDirectory 'startup-baseline.txt'),"Serial: $script:serial`nPackage: $script:package`nInitial PID: $startPid`nDevice epoch: $script:deviceStartEpoch`nIterations: $iterations",[Text.UTF8Encoding]::new($false))
$failure = $null
try {
    [void](Wait-ForHome 0)
    for ($cycle=1; $cycle -le $iterations; $cycle++) {
        Terminate-App
        Wait-ForStopped $cycle
        Activate-App
        [void](Wait-ForHome $cycle)
        $launchPid = Get-AppProcessId
        if (-not $launchPid) { throw "No app PID after Home appeared in launch cycle $cycle." }
        if (-not $script:processIds.Contains($launchPid)) { $script:processIds.Add($launchPid) }
        Start-Sleep -Milliseconds 3000
        $heldPid = Get-AppProcessId
        if ($heldPid -ne $launchPid) { throw "App process did not survive the 3-second startup hold in cycle $cycle (before $launchPid, after $heldPid)." }

        Press-Home
        $backgroundPid = Get-AppProcessId
        if ($backgroundPid -ne $launchPid) { throw "App process changed after HOME in cycle $cycle (before $launchPid, after $backgroundPid)." }
        Activate-App
        [void](Wait-ForHome $cycle)
        $resumePid = Get-AppProcessId
        if ($resumePid -ne $launchPid) { throw "App process changed after resume in cycle $cycle (before $launchPid, after $resumePid)." }
        if ($cycle % 5 -eq 0) { Write-Host "Startup cycles completed: $cycle of $iterations." }
    }
    $log = Save-UnfilteredLogcat 'startup-logcat.log'
    $fatal = Count-AppFatal $log
    if ($fatal.Managed -or $fatal.Native) { throw "App fatal traces found: $($fatal.Managed) managed, $($fatal.Native) native; inspect startup-logcat.log." }
} catch {
    $failure = $_
    # Capture the full, unfiltered log before any further UI action or retry.
    try { Save-UnfilteredLogcat 'startup-failure-logcat.log' | Out-Null }
    catch { Write-Warning "Could not save unfiltered logcat after startup failure: $($_.Exception.Message)" }
}
if ($failure) { throw $failure }
Write-Host "PASS startup stability on $($context.AvdName) ($script:serial); $iterations force-stop/launch and HOME/resume cycles; observed PID(s): $($script:processIds -join ', ')."
