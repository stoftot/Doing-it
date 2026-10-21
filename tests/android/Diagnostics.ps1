param([Parameter(Mandatory=$true)][string]$ContextPath,[hashtable]$TestParameters=@{})

$ErrorActionPreference = 'Stop'
$context = Get-Content -LiteralPath $ContextPath -Raw | ConvertFrom-Json
if (-not $context.SessionUrl) { throw 'Diagnostics requires the runner-owned Managed Appium session.' }
if (-not $context.Serial) { throw 'Diagnostics requires the reserved device serial in runner context.' }
$script:sessionUrl = [string]$context.SessionUrl
$script:serial = [string]$context.Serial
$script:outputDirectory = [string]$context.OutputDirectory
$script:package = 'com.doingit.app'
$managedCrash = $false
if ($TestParameters.ContainsKey('ManagedCrash')) { $managedCrash = [bool]$TestParameters.ManagedCrash }
$verifyHooksAbsent = $false
if ($TestParameters.ContainsKey('VerifyHooksAbsent')) { $verifyHooksAbsent = [bool]$TestParameters.VerifyHooksAbsent }
if ($managedCrash -and $verifyHooksAbsent) { throw 'ManagedCrash and VerifyHooksAbsent are mutually exclusive.' }
$script:processIds = [System.Collections.Generic.List[string]]::new()

function Invoke-Driver([string]$Method,[string]$Path,$Body=$null) {
    $request = @{ Uri = "$script:sessionUrl$Path"; Method = $Method; TimeoutSec = 30 }
    if ($null -ne $Body) { $request.ContentType = 'application/json; charset=utf-8'; $request.Body = ConvertTo-Json -InputObject $Body -Depth 20 -Compress }
    Invoke-RestMethod @request
}
function Invoke-Adb([string[]]$Arguments) {
    $result = & adb -s $script:serial @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) { throw "adb -s $script:serial $($Arguments -join ' ') failed ($LASTEXITCODE): $result" }
    [string]::Join("`n", @($result | ForEach-Object { [string]$_ }))
}
function Find-XPath([string]$XPath) {
    $element = (Invoke-Driver 'POST' '/element' @{ using='xpath'; value=$XPath }).value
    $id = $element.'element-6066-11e4-a52e-4f735466cecf'; if (-not $id) { $id = $element.ELEMENT }
    if (-not $id) { throw "No element identifier returned for $XPath" }; [string]$id
}
function Try-Find-XPath([string]$XPath) { try { Find-XPath $XPath } catch { $null } }
function Find-ById([string]$Id) { Find-XPath "//*[contains(@resource-id, ':id/$Id') or @resource-id='com.doingit.app:id/$Id' or @content-desc='$Id' or @name='$Id']" }
function Try-FindById([string]$Id) { try { Find-ById $Id } catch { $null } }
function Find-ByText([string]$Text) {
    $literal = if (-not $Text.Contains("'")) { "'$Text'" } elseif (-not $Text.Contains('"')) { '"' + $Text + '"' } else { throw "Unsupported XPath text: $Text" }
    Find-XPath "//*[@text=$literal or @content-desc=$literal or @name=$literal]"
}
function Get-Source { [string](Invoke-Driver 'GET' '/source').value }
function Click-Element([string]$Element) { Invoke-Driver 'POST' "/element/$Element/click" @{} | Out-Null }
function Click-Id([string]$Id) { Click-Element (Find-ById $Id) }
function Wait-For([scriptblock]$Condition,[string]$Description,[int]$Attempts=30,[int]$DelayMs=300) {
    for ($i=0; $i -lt $Attempts; $i++) { if (& $Condition) { return }; Start-Sleep -Milliseconds $DelayMs }
    throw "Timed out waiting for $Description."
}
function Get-AppProcessId {
    $result = & adb -s $script:serial shell pidof $script:package 2>&1
    if ($LASTEXITCODE -ne 0) { return '' }
    $value = [string]::Join(' ',@($result | ForEach-Object { [string]$_ })).Trim()
    if (-not $value) { return '' }; ($value -split '\s+')[0]
}
function Save-Source([string]$Name) {
    [IO.File]::WriteAllText((Join-Path $script:outputDirectory $Name), (Get-Source), [Text.UTF8Encoding]::new($false))
}
function Save-Logcat([string]$Name) {
    $log = Invoke-Adb @('logcat','-d','-v','epoch','-T',"$script:deviceStartEpoch.000")
    [IO.File]::WriteAllText((Join-Path $script:outputDirectory $Name), $log, [Text.UTF8Encoding]::new($false))
    $log
}
function Assert-True([bool]$Condition,[string]$Message) { if (-not $Condition) { throw $Message } }
function Wait-ForHome {
    Wait-For { (Get-Source).Contains('TabHome') } 'Home after app activation' 40 500
}
function Open-Diagnostics {
    Click-Id 'OpenDiagnostics'
    Wait-For { (Try-FindById 'DiagnosticCount') -ne $null } 'diagnostics list'
}
function Get-ReportCount {
    $source = Get-Source
    if ($source -notmatch '(\d+) saved reports') { throw 'DiagnosticCount did not expose an "N saved reports" value.' }
    [int]$Matches[1]
}
function Find-ReportView([string[]]$Kinds) {
    # Match the native kind label, then use its report ID to select that exact
    # card's button. Scroll the diagnostics list when the target card is below view.
    for ($attempt=0; $attempt -lt 12; $attempt++) {
        [xml]$document = Get-Source
        foreach ($node in $document.SelectNodes('//*[@resource-id]')) {
            $resourceId = $node.GetAttribute('resource-id')
            if ($resourceId -notmatch 'DiagnosticKind_(.+)$') { continue }
            $reportId = $Matches[1]
            $label = $node.GetAttribute('text')
            if (-not $label) { $label = $node.GetAttribute('content-desc') }
            if ($Kinds -notcontains $label) { continue }
            $view = Try-FindById "ViewDiagnostic_$reportId"
            if ($view) { return @{ Element=$view; Kind=$label } }
        }
        if ($attempt -eq 11) { break }
        $scroll = Try-Find-XPath '//android.widget.ScrollView[@scrollable="true"]'
        if (-not $scroll) { break }
        Invoke-Driver 'POST' '/execute/sync' @{ script='mobile: scrollGesture'; args=@(@{ elementId=$scroll; direction='down'; percent=0.7 }) } | Out-Null
        Start-Sleep -Milliseconds 250
    }
    $null
}
function Read-ReportDetail([string[]]$Kinds) {
    $report = Find-ReportView $Kinds
    if (-not $report) { throw "Could not find a View report control for kind '$($Kinds -join ' or ')'." }
    Click-Element $report.Element
    Wait-For { (Try-FindById 'DiagnosticDetails') -ne $null } "details for $($report.Kind) report"
    $source = Get-Source
    Save-Source 'diagnostics-report-detail.xml'
    $source
}
function Close-Detail {
    Click-Element (Find-XPath "//*[@content-desc='Close']")
    Wait-For { (Try-FindById 'DiagnosticCount') -ne $null } 'diagnostics list after closing details'
}
function Press-Back { Invoke-Driver 'POST' '/appium/device/press_keycode' @{ keycode=4 } | Out-Null }
if (-not (Test-Path -LiteralPath $script:outputDirectory)) { throw 'Runner OutputDirectory is missing.' }
$script:deviceStartEpoch = (Invoke-Adb @('shell','date','+%s')).Trim()
if ($script:deviceStartEpoch -notmatch '^\d+$') { throw "Could not read device epoch time: $script:deviceStartEpoch" }
$initialPid = Get-AppProcessId
if (-not $initialPid) { throw 'App process was not running when diagnostics suite began.' }
$script:processIds.Add($initialPid)
$failure = $null
try {
    $null = Invoke-Driver 'POST' '/timeouts' @{ implicit=500 }
    [IO.File]::WriteAllText((Join-Path $script:outputDirectory 'diagnostics-baseline.txt'), "Serial: $script:serial`nPackage: $script:package`nInitial PID: $initialPid`nDevice epoch: $script:deviceStartEpoch`nManagedCrash: $managedCrash`nVerifyHooksAbsent: $verifyHooksAbsent", [Text.UTF8Encoding]::new($false))
    [void](Find-ById 'TabHome')
    Open-Diagnostics
    $baselineCount = Get-ReportCount
    $script:baselineCount = $baselineCount
    Save-Source 'diagnostics-baseline.xml'
    Click-Element (Find-XPath "//*[@content-desc='Close']")
    Wait-For { (Try-FindById 'TabHome') -ne $null } 'Home before inducing app crash'

    if ($verifyHooksAbsent) {
        $activity = [string]$context.Capabilities.'appium:appActivity'
        if (-not $activity) { throw 'The runner did not provide the resolved launcher activity.' }
        Invoke-Adb @('shell','am','start','-W','-n',"$script:package/$activity",'-a','com.doingit.app.TEST_MANAGED_CRASH') | Out-Null
        Start-Sleep -Seconds 2
        $afterHookProbePid = Get-AppProcessId
        Assert-True ($afterHookProbePid -eq $initialPid) "TEST_MANAGED_CRASH unexpectedly changed PID (initial $initialPid, current $afterHookProbePid); hook may be present in this build."
        Save-Source 'diagnostics-hooks-absent.xml'
        Write-Host "PASS diagnostics hooks absent on $($context.AvdName) ($script:serial); PID remained $initialPid."
    } else {
        $activity = [string]$context.Capabilities.'appium:appActivity'
        if (-not $activity) { throw 'The runner did not provide the resolved launcher activity.' }
        if ($managedCrash) {
            Invoke-Adb @('shell','am','start','-W','-n',"$script:package/$activity",'-a','com.doingit.app.TEST_MANAGED_CRASH') | Out-Null
        } else {
            # This intentionally crashes only Doing it; no shared device data is reset.
            Invoke-Adb @('shell','am','crash',$script:package) | Out-Null
        }
        Wait-For { -not (Get-AppProcessId) } 'Doing it process to exit after induced crash' 30 250

        # Android can leave its crash dialog above Home. Dismiss only that dialog.
        $systemSource = Get-Source
        if ($systemSource -match 'Close app|App keeps stopping|has stopped') {
            $closeApp = Try-Find-XPath "//*[@text='Close app' or @content-desc='Close app' or @text='OK']"
            if ($closeApp) { Click-Element $closeApp } else { Press-Back }
        }
        Invoke-Driver 'POST' '/appium/device/activate_app' @{ appId=$script:package } | Out-Null
        Wait-ForHome
        $postCrashPid = Get-AppProcessId
        if ($postCrashPid -and -not $script:processIds.Contains($postCrashPid)) { $script:processIds.Add($postCrashPid) }
        Open-Diagnostics
        $countAfterCrash = Get-ReportCount
        $expectedCountAfterCrash = [Math]::Min(20, $baselineCount + 1)
        Assert-True ($countAfterCrash -ge $expectedCountAfterCrash) "Report count did not reflect the induced crash (baseline $baselineCount, now $countAfterCrash)."
        $source = Get-Source
        Save-Source 'diagnostics-after-crash.xml'
        if (-not $managedCrash) {
            Assert-True ($source.Contains('Android crash')) 'The saved report list did not contain an Android crash report.'
            $systemDetail = Read-ReportDetail @('Android crash')
            Assert-True ($systemDetail.Contains($initialPid)) "Android crash detail did not contain the pre-crash PID $initialPid."
            Assert-True ($systemDetail -match 'Build UTC') 'Android crash detail omitted Build UTC.'
            Assert-True ($systemDetail -match 'PID') 'Android crash detail omitted PID metadata.'
            Close-Detail
        }

        # Export opens Android's chooser. Inspect its visible package-independent
        # filename, then press Back so no destination receives the report.
        Click-Id 'ShareDiagnostics'
        Wait-For { (Get-Source) -match 'diagnostics-[^<\s"'']+\.zip' } 'diagnostics ZIP in Android share chooser' 30 400
        Save-Source 'diagnostics-share-chooser.xml'
        Press-Back
        Wait-For { (Try-FindById 'DiagnosticCount') -ne $null } 'diagnostics list after canceling share chooser'

        $beforeCancelDelete = Get-ReportCount
        Click-Id 'ClearDiagnostics'
        Wait-For { (Get-Source) -match 'Delete saved reports\?' } 'delete confirmation'
        $keep = Try-Find-XPath "//*[@text='Keep reports' or @content-desc='Keep reports']"
        if (-not $keep) { throw 'Delete confirmation did not expose Keep reports.' }
        Click-Element $keep
        Wait-For { (Try-FindById 'DiagnosticCount') -ne $null } 'diagnostics after canceling delete'
        Assert-True ((Get-ReportCount) -eq $beforeCancelDelete) 'Canceling delete changed the saved report count.'
        Save-Source 'diagnostics-after-cancel-delete.xml'

        # Reopen and restart Doing it to prove app-owned report persistence.
        Click-Element (Find-XPath "//*[@content-desc='Close']")
        Wait-For { (Try-FindById 'TabHome') -ne $null } 'Home after diagnostics close'
        Click-Id 'OpenDiagnostics'; Wait-For { (Try-FindById 'DiagnosticCount') -ne $null } 'diagnostics after reopen'
        Assert-True ((Get-ReportCount) -eq $beforeCancelDelete) 'Report count changed after reopening diagnostics.'
        Save-Source 'diagnostics-after-reopen.xml'
        Click-Element (Find-XPath "//*[@content-desc='Close']")
        Wait-For { (Try-FindById 'TabHome') -ne $null } 'Home before app restart'
        Invoke-Driver 'POST' '/appium/device/terminate_app' @{ appId=$script:package } | Out-Null
        Wait-For { -not (Get-AppProcessId) } 'Doing it process to stop for restart' 20 250
        Invoke-Driver 'POST' '/appium/device/activate_app' @{ appId=$script:package } | Out-Null
        Wait-ForHome
        $restartPid = Get-AppProcessId
        if ($restartPid -and -not $script:processIds.Contains($restartPid)) { $script:processIds.Add($restartPid) }
        Open-Diagnostics
        Assert-True ((Get-ReportCount) -eq $beforeCancelDelete) 'Saved report count did not persist across app restart.'
        if (-not $managedCrash) { Assert-True ((Get-Source).Contains('Android crash')) 'Android crash report did not persist across app restart.' }
        Save-Source 'diagnostics-after-restart.xml'
        $finalCount = Get-ReportCount
        if ($managedCrash) {
            $managedDetail = Read-ReportDetail @('Unhandled managed exception','Unhandled Android callback')
            Assert-True ($managedDetail.Contains('InvalidOperationException')) 'Managed report omitted InvalidOperationException.'
            Assert-True ($managedDetail.Contains($initialPid)) "Managed report did not contain the pre-crash PID $initialPid."
            Assert-True ($managedDetail -match 'Build UTC') 'Managed report omitted Build UTC.'
            Assert-True ($managedDetail.Contains('MainActivity.OnNewIntent')) 'Managed report omitted the OnNewIntent call-stack evidence.'
            Assert-True (-not $managedDetail.Contains('PRIVATE-TEST-MESSAGE')) 'Managed report leaked the private test exception message.'
            Close-Detail
        }
        Assert-True ($finalCount -ge [Math]::Min(20, $baselineCount + 1)) 'Final diagnostic count did not reflect the induced crash record.'
        if (Try-FindById 'DiagnosticCount') {
            Click-Element (Find-XPath "//*[@content-desc='Close']")
            Wait-For { (Try-FindById 'TabHome') -ne $null } 'Home after diagnostics suite'
        }
    }
    Wait-For { (Try-FindById 'TabHome') -ne $null } 'Home at diagnostics suite completion'
} catch {
    $failure = $_
    try { Save-Source 'diagnostics-failure.xml' } catch { Write-Warning "Could not capture diagnostics failure source: $($_.Exception.Message)" }
    try { Save-Logcat 'diagnostics-failure-logcat.log' | Out-Null } catch { Write-Warning "Could not capture unfiltered diagnostics logcat: $($_.Exception.Message)" }
}

try { Save-Logcat 'diagnostics-logcat.log' | Out-Null }
catch { if (-not $failure) { $failure = $_ } else { Write-Warning "Could not save final diagnostics logcat: $($_.Exception.Message)" } }
if ($failure) { throw $failure }
if ($verifyHooksAbsent) {
    Write-Host "PASS diagnostics hooks absent on $($context.AvdName) ($script:serial); PID remained $initialPid; baseline reports preserved."
} elseif ($managedCrash) {
    Write-Host "PASS managed diagnostics on $($context.AvdName) ($script:serial); initial PID $initialPid; $finalCount reports persisted; private message omitted."
} else {
    Write-Host "PASS Android crash diagnostics on $($context.AvdName) ($script:serial); initial PID $initialPid; $finalCount reports persisted; share and delete canceled."
}
