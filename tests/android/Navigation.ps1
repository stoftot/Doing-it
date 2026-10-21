param([Parameter(Mandatory=$true)][string]$ContextPath,[hashtable]$TestParameters=@{})

$ErrorActionPreference = 'Stop'
$context = Get-Content -LiteralPath $ContextPath -Raw | ConvertFrom-Json
if (-not $context.SessionUrl) { throw 'Navigation requires the runner-owned Managed Appium session.' }
if (-not $context.Serial) { throw 'Navigation requires the reserved device serial in runner context.' }
$script:sessionUrl = [string]$context.SessionUrl
$script:serial = [string]$context.Serial
$script:outputDirectory = [string]$context.OutputDirectory
$script:package = 'com.doingit.app'
$iterations = 8
if ($TestParameters.ContainsKey('Iterations')) { $iterations = [int]$TestParameters.Iterations }
if ($iterations -lt 1 -or $iterations -gt 30) { throw 'Iterations must be between 1 and 30.' }
$baselineProbe = $false
if ($TestParameters.ContainsKey('BaselineProbe')) { $baselineProbe = [bool]$TestParameters.BaselineProbe }
$runId = [guid]::NewGuid().ToString('N').Substring(0,10)
$fixture = "QA-NAV-$runId"
$script:fixtureCreated = $false

function Invoke-Driver([string]$Method,[string]$Path,$Body=$null) {
    $request = @{ Uri = "$script:sessionUrl$Path"; Method = $Method; TimeoutSec = 60 }
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
function Find-ById([string]$Id) { Find-XPath "//*[contains(@resource-id, ':id/$Id') or @resource-id='com.doingit.app:id/$Id' or @content-desc='$Id' or @name='$Id']" }
function Try-FindById([string]$Id) { try { Find-ById $Id } catch { $null } }
function Find-ByText([string]$Text) {
    $literal = if (-not $Text.Contains("'")) { "'$Text'" } elseif (-not $Text.Contains('"')) { '"' + $Text + '"' } else { throw "Unsupported XPath text: $Text" }
    Find-XPath "//*[@text=$literal or @content-desc=$literal or @name=$literal]"
}
function Get-Source { [string](Invoke-Driver 'GET' '/source').value }
function Count-ExactText([string]$Source,[string]$Text) {
    [xml]$document = $Source
    $count = 0
    foreach ($node in $document.SelectNodes('//*[@text]')) { if ($node.GetAttribute('text') -ceq $Text) { $count++ } }
    $count
}
function Click-Id([string]$Id) { Invoke-Driver 'POST' "/element/$(Find-ById $Id)/click" @{} | Out-Null; Start-Sleep -Milliseconds 250 }
function Click-Element([string]$Element) { Invoke-Driver 'POST' "/element/$Element/click" @{} | Out-Null; Start-Sleep -Milliseconds 250 }
function Set-ImplicitWait([int]$Milliseconds) { Invoke-Driver 'POST' '/timeouts' @{ implicit=$Milliseconds } | Out-Null }
function Find-ScrollableText([string]$Text) {
    for ($attempt=0; $attempt -lt 8; $attempt++) {
        try { return Find-ByText $Text } catch { }
        $scroll = Find-XPath '//android.widget.ScrollView[@scrollable="true"]'
        Invoke-Driver 'POST' '/execute/sync' @{ script='mobile: scrollGesture'; args=@(@{ elementId=$scroll; direction='down'; percent=0.75 }) } | Out-Null
        Start-Sleep -Milliseconds 250
    }
    throw "Could not bring '$Text' into the visible app area after scrolling."
}
function Get-Center([string]$Element) {
    $r = (Invoke-Driver 'GET' "/element/$Element/rect").value
    @{ x=[int]([int]$r.x + [int]$r.width/2); y=[int]([int]$r.y + [int]$r.height/2) }
}
function Invoke-TapBurst([string]$Element,[int]$Count=5) {
    $p = Get-Center $Element
    $actions = [System.Collections.Generic.List[object]]::new()
    $actions.Add(@{ type='pointerMove'; duration=0; x=$p.x; y=$p.y })
    for ($i=0; $i -lt $Count; $i++) {
        $actions.Add(@{ type='pointerDown'; button=0 }); $actions.Add(@{ type='pointerUp'; button=0 })
        if ($i -lt $Count-1) { $actions.Add(@{ type='pause'; duration=20 }) }
    }
    Invoke-Driver 'POST' '/actions' @{ actions=@(@{ type='pointer'; id='nav-pointer'; parameters=@{ pointerType='touch' }; actions=@($actions.ToArray()) }) } | Out-Null
    try { Invoke-Driver 'DELETE' '/actions' | Out-Null } catch { }
}
function Invoke-CloseBackRace([int]$X,[int]$Y) {
    # Appium serializes separate HTTP commands. Inject on the reserved device
    # concurrently so Back overlaps the modal animation instead of closing the
    # parent after that animation has already completed. Coordinates are integers.
    Invoke-Adb @('shell', "input tap $X $Y & input keyevent 4; wait") | Out-Null
}
function Assert-True([bool]$Condition,[string]$Message) { if (-not $Condition) { throw $Message } }
function Wait-ForId([string]$Id) {
    for ($i=0; $i -lt 20; $i++) { if (Try-FindById $Id) { return }; Start-Sleep -Milliseconds 200 }
    throw "Expected visible control '$Id'."
}
function Open-BurstCloseOnce([string]$OpenId,[string]$ParentId,[string]$Name) {
    $open = Find-ById $OpenId
    Invoke-TapBurst $open
    Wait-ForId 'TaskTitle'
    Click-Element (Find-XPath "//*[@content-desc='Close']")
    Wait-ForId $ParentId
    Assert-True (-not (Try-FindById 'TaskTitle')) "$Name left another editor open after one Close."
}
function Open-SingleCloseBurst([string]$OpenId,[string]$ParentId,[string]$Name) {
    Click-Id $OpenId
    Wait-ForId 'TaskTitle'
    Invoke-TapBurst (Find-XPath "//*[@content-desc='Close']")
    Wait-ForId $ParentId
    Assert-True (-not (Try-FindById 'TaskTitle')) "$Name did not return to its parent after the Close burst."
}
function Get-AppProcessId {
    $value = (Invoke-Adb @('shell','pidof',$script:package)).Trim()
    if (-not $value) { return '' }; ($value -split '\s+')[0]
}
function Save-Probe([string]$Name) {
    [IO.File]::WriteAllText((Join-Path $script:outputDirectory "$Name.xml"), (Get-Source), [Text.UTF8Encoding]::new($false))
}

if (-not (Test-Path -LiteralPath $script:outputDirectory)) { throw 'Runner OutputDirectory is missing.' }
$script:deviceStartEpoch = (Invoke-Adb @('shell','date','+%s')).Trim()
if ($script:deviceStartEpoch -notmatch '^\d+$') { throw "Could not read device epoch time: $script:deviceStartEpoch" }
$initialPid = Get-AppProcessId
if (-not $initialPid) { throw 'App process was not running when navigation suite began.' }
$initialCrashes = Invoke-Adb @('logcat','-d','-v','epoch','-s','AndroidRuntime:E')
$baselineRecord = "Serial: $script:serial`nPackage: $script:package`nPID: $initialPid`nDevice epoch: $script:deviceStartEpoch`n`n$initialCrashes"
[IO.File]::WriteAllText((Join-Path $script:outputDirectory 'navigation-crash-baseline.log'), $baselineRecord, [Text.UTF8Encoding]::new($false))
$null = Invoke-Driver 'POST' '/timeouts' @{ implicit=500 }
$failure = $null
$cleanupFailure = $null
try {
    [void](Find-ById 'TabHome')
    if ($baselineProbe) {
        Open-BurstCloseOnce 'AddTodayTask' 'TabHome' 'Baseline AddTodayTask'
        Assert-True ((Get-AppProcessId) -eq $initialPid) 'App process changed during baseline navigation probe.'
    } else {
        for ($i=1; $i -le $iterations; $i++) {
            Click-Id 'TabHome'; Open-BurstCloseOnce 'AddTodayTask' 'TabHome' "Today editor iteration $i"
            Click-Id 'TabLists'; Open-BurstCloseOnce 'CreateTask' 'TabLists' "List editor iteration $i"
            Click-Id 'TabRecurring'; Open-BurstCloseOnce 'CreateRecurringTask' 'TabRecurring' "Recurring editor iteration $i"
            Click-Id 'TabHome'; Click-Id 'Opencalendar'; Wait-ForId 'AddCalendarTask'
            Open-BurstCloseOnce 'AddCalendarTask' 'AddCalendarTask' "Calendar editor iteration $i"
            Click-Element (Find-XPath "//*[@content-desc='Close']")
            Wait-ForId 'TabHome'
            $appProcessId = Get-AppProcessId
            Assert-True ($appProcessId -eq $initialPid) "App process changed after navigation iteration $i (initial $initialPid, current $appProcessId)."
        }

        # A close burst is isolated from the open burst: only one editor was opened.
        Click-Id 'TabHome'; Click-Id 'Opencalendar'; Wait-ForId 'AddCalendarTask'
        Open-SingleCloseBurst 'AddCalendarTask' 'AddCalendarTask' 'Calendar Close burst'
        Click-Element (Find-XPath "//*[@content-desc='Close']"); Wait-ForId 'TabHome'

        # Dispatch Close and Back together from a nested editor; Calendar must remain on top.
        Click-Id 'TabHome'; Click-Id 'Opencalendar'; Wait-ForId 'AddCalendarTask'
        Click-Id 'AddCalendarTask'; Wait-ForId 'TaskTitle'
        $closePoint = Get-Center (Find-XPath "//*[@content-desc='Close']")
        Invoke-CloseBackRace $closePoint.x $closePoint.y
        Wait-ForId 'AddCalendarTask'
        Assert-True (-not (Try-FindById 'TaskTitle')) 'Concurrent Close/Back dismissed past the Calendar parent.'
        Click-Element (Find-XPath "//*[@content-desc='Close']"); Wait-ForId 'TabHome'
        Assert-True ((Get-AppProcessId) -eq $initialPid) 'App process changed after Close and Back race.'

        Invoke-TapBurst (Find-ById 'Notificationsettings')
        Wait-ForId 'DailyNotification'
        Invoke-TapBurst (Find-XPath "//*[@content-desc='Close']")
        Wait-ForId 'TabHome'

        # Exercise the actual launcher intent route without changing widget placement.
        $activity = [string]$context.Capabilities.'appium:appActivity'
        if (-not $activity) { throw 'The runner did not provide the resolved launcher activity.' }
        Click-Id 'Opencalendar'; Wait-ForId 'AddCalendarTask'
        Invoke-Adb @('shell','am','start','-W','-n',"$script:package/$activity",'-a','com.doingit.app.CREATE_TASK') | Out-Null
        Wait-ForId 'TaskTitle'
        $draft = Find-ById 'TaskTitle'
        Invoke-Driver 'POST' "/element/$draft/value" @{ text=$fixture; value=@($fixture.ToCharArray() | ForEach-Object { [string]$_ }) } | Out-Null
        for ($shortcut=0; $shortcut -lt 3; $shortcut++) {
            Invoke-Adb @('shell','am','start','-W','-n',"$script:package/$activity",'-a','com.doingit.app.CREATE_TASK') | Out-Null
        }
        Assert-True ((Get-Source).Contains($fixture)) 'Repeated shortcut navigation discarded the open draft.'
        Click-Element (Find-XPath "//*[@content-desc='Close']"); Wait-ForId 'TabHome'
        Assert-True (-not (Get-Source).Contains($fixture)) 'Closing the shortcut editor saved its draft.'

        # Invalid Save retains the editor; a repeated valid Save must persist one unique task.
        Click-Id 'TabLists'; Click-Id 'CreateTask'; Wait-ForId 'TaskTitle'
        Click-Id 'SaveTask'
        [void](Find-ByText "Couldn't save")
        Assert-True ((Get-Source).Contains('Task title is required.')) 'Blank save did not show required-title validation.'
        Click-Element (Find-ByText 'OK')
        Wait-ForId 'TaskTitle'
        $input = Find-ById 'TaskTitle'
        Invoke-Driver 'POST' "/element/$input/value" @{ text=$fixture; value=@($fixture.ToCharArray() | ForEach-Object { [string]$_ }) } | Out-Null
        $script:fixtureCreated = $true
        Invoke-TapBurst (Find-ById 'SaveTask') 5
        Wait-ForId 'TabLists'
        $source = Get-Source
        Assert-True ($source.Contains($fixture)) 'Saved navigation fixture did not appear in Lists.'
        Assert-True ((Count-ExactText $source $fixture) -eq 1) 'Repeated Save created duplicate visible fixture rows.'
        Save-Probe 'navigation-after-save'

        Click-Element (Find-XPath "//*[@content-desc='Edit $fixture']")
        Wait-ForId 'TaskTitle'
        Click-Element (Find-ScrollableText 'Delete task')
        [void](Find-ByText 'Delete this task?')
        Click-Element (Find-ByText 'Keep task')
        Wait-ForId 'TaskTitle'
        Click-Element (Find-XPath "//*[@content-desc='Close']"); Wait-ForId 'TabLists'
        Assert-True ((Count-ExactText (Get-Source) $fixture) -eq 1) 'Canceling Delete removed or duplicated the task.'
    }
} catch {
    $failure = $_
    try { Save-Probe 'navigation-failure' } catch { Write-Warning "Could not capture navigation failure source: $($_.Exception.Message)" }
}
finally {
    try {
        $finalCrashes = Invoke-Adb @('logcat','-d','-v','epoch','-T',"$script:deviceStartEpoch.000",'-s','AndroidRuntime:E')
        [IO.File]::WriteAllText((Join-Path $script:outputDirectory 'navigation-crash-final.log'), $finalCrashes, [Text.UTF8Encoding]::new($false))
        if ($script:fixtureCreated) {
            if (-not (Try-FindById 'TabLists')) { throw "Cannot safely clean up test-owned task '$fixture'; Lists is not visible." }
            for ($cleanupAttempt=0; $cleanupAttempt -lt 10; $cleanupAttempt++) {
                $source = Get-Source
                if (-not $source.Contains($fixture)) { break }
                $row = Find-ScrollableText "Edit $fixture"
                Invoke-Driver 'POST' "/element/$row/click" @{} | Out-Null
                Wait-ForId 'TaskTitle'
                $delete = Find-ScrollableText 'Delete task'
                Invoke-Driver 'POST' "/element/$delete/click" @{} | Out-Null
                [void](Find-XPath "//*[@text='Delete this task?']")
                $confirm = Find-XPath "//*[@text='Delete']"
                Invoke-Driver 'POST' "/element/$confirm/click" @{} | Out-Null
                Wait-ForId 'TabLists'
            }
            Assert-True (-not (Get-Source).Contains($fixture)) "Test-owned task '$fixture' remained after cleanup."
        }
        $crashLines = @($finalCrashes -split '\r?\n')
        $appFatalCount = 0
        for ($lineIndex=0; $lineIndex -lt $crashLines.Count; $lineIndex++) {
            if ($crashLines[$lineIndex] -notmatch 'FATAL EXCEPTION:') { continue }
            $lastRelevantLine = [Math]::Min($lineIndex + 4, $crashLines.Count - 1)
            $block = ($crashLines[$lineIndex..$lastRelevantLine] -join "`n")
            if ($block -match 'Process:\s*com\.doingit\.app,\s*PID:\s*\d+') { $appFatalCount++ }
        }
        if ($appFatalCount -gt 0) { throw "Found $appFatalCount app fatal crash entries since device epoch $script:deviceStartEpoch; inspect navigation-crash-final.log." }
        $finalAppProcessId = Get-AppProcessId
        if ($finalAppProcessId -ne $initialPid) { throw "App process changed during suite (initial $initialPid, final $finalAppProcessId)." }
    } catch { $cleanupFailure = $_ }
}
if ($failure) { if ($cleanupFailure) { throw "Navigation suite failed: $($failure.Exception.Message) Cleanup/diagnostics also failed: $($cleanupFailure.Exception.Message)" }; throw $failure }
if ($cleanupFailure) { throw $cleanupFailure }
$runDescription = if ($baselineProbe) { 'baseline probe' } else { "$iterations iterations" }
Write-Host "PASS navigation bursts on $($context.AvdName) ($script:serial); $runDescription; fixture $runId cleaned."
