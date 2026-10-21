param([Parameter(Mandatory=$true)][string]$ContextPath,[hashtable]$TestParameters=@{})

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$context = Get-Content -LiteralPath $ContextPath -Raw | ConvertFrom-Json
if (-not $context.SessionUrl) { throw 'DailyNotifications requires the runner-owned Managed Appium session.' }
if (-not $context.Serial) { throw 'DailyNotifications requires the reserved device serial in runner context.' }
$script:sessionUrl = [string]$context.SessionUrl
$script:outputDirectory = [string]$context.OutputDirectory
$script:serial = [string]$context.Serial
$script:fixtureTitle = "QA-NOTIFY-$([guid]::NewGuid().ToString('N').Substring(0,10))"
$script:fixtureCreated = $false
$script:preconditionPassed = $false
$script:permissionInitiallyGranted = $false
$script:priorRemainingCount = 0
$script:exactAlarmMode = $null

function Invoke-Driver([string]$Method,[string]$Path,$Body=$null) {
    $request = @{ Uri = "$script:sessionUrl$Path"; Method = $Method; TimeoutSec = 60 }
    if ($null -ne $Body) {
        $request.ContentType = 'application/json; charset=utf-8'
        $request.Body = ConvertTo-Json -InputObject $Body -Depth 20 -Compress
    }
    try { Invoke-RestMethod @request }
    catch { throw "$Method $Path $($request.Body): $($_.Exception.Message)" }
}

function Invoke-Adb([string[]]$Arguments) {
    $result = & adb -s $script:serial @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) { throw "adb -s $script:serial $($Arguments -join ' ') failed ($LASTEXITCODE): $result" }
    [string]::Join("`n", @($result | ForEach-Object { [string]$_ }))
}

function Find-XPath([string]$XPath) {
    $response = Invoke-Driver 'POST' '/element' @{ using = 'xpath'; value = $XPath }
    $element = $response.value
    $id = $element.'element-6066-11e4-a52e-4f735466cecf'
    if (-not $id) { $id = $element.ELEMENT }
    if (-not $id) { throw "No element identifier returned for XPath: $XPath" }
    [string]$id
}

function Find-ById([string]$Id) {
    Find-XPath "//*[contains(@resource-id, ':id/$Id') or @resource-id='com.doingit.app:id/$Id' or @content-desc='$Id' or @name='$Id']"
}

function Find-ByText([string]$Text) {
    $literal = if (-not $Text.Contains("'")) { "'$Text'" } elseif (-not $Text.Contains('"')) { '"' + $Text + '"' } else { throw "Unsupported XPath text: $Text" }
    Find-XPath "//*[@text=$literal or @content-desc=$literal or @name=$literal]"
}

function Try-FindByText([string]$Text) { try { Find-ByText $Text } catch { $null } }
function Try-FindById([string]$Id) { try { Find-ById $Id } catch { $null } }
function Click-Element([string]$Id) { Invoke-Driver 'POST' "/element/$Id/click" @{} | Out-Null; Start-Sleep -Milliseconds 350 }
function Click-ById([string]$Id) { Click-Element (Find-ById $Id) }
function Click-ByText([string]$Text) { Click-Element (Find-ByText $Text) }
function Get-Text([string]$Id) { [string](Invoke-Driver 'GET' "/element/$Id/text").value }
function Get-Attribute([string]$Id,[string]$Name) { [string](Invoke-Driver 'GET' "/element/$Id/attribute/$Name").value }
function Get-PageSource { [string](Invoke-Driver 'GET' '/source').value }

function Save-Probe([string]$Name) {
    $source = Get-PageSource
    [IO.File]::WriteAllText((Join-Path $script:outputDirectory "$Name.xml"), $source, [Text.UTF8Encoding]::new($false))
    $shot = Invoke-Driver 'GET' '/screenshot'
    [IO.File]::WriteAllBytes((Join-Path $script:outputDirectory "$Name.png"), [Convert]::FromBase64String([string]$shot.value))
    $source
}

function Assert-True([bool]$Condition,[string]$Message) { if (-not $Condition) { throw $Message } }
function Assert-Contains([string]$Text,[string]$Needle,[string]$Message) {
    if (-not $Text.Contains($Needle,[StringComparison]::Ordinal)) { throw $Message }
}
function Assert-NotContains([string]$Text,[string]$Needle,[string]$Message) {
    if ($Text.Contains($Needle,[StringComparison]::Ordinal)) { throw $Message }
}

function Open-NotificationShade {
    # Restoring a disabled keyguard can finish by closing the shade. Wait for
    # that system transition and retry opening, while still requiring its UI.
    for ($attempt=0; $attempt -lt 3; $attempt++) {
        Invoke-Driver 'POST' '/execute/sync' @{ script='mobile: openNotifications'; args=@() } | Out-Null
        Start-Sleep -Milliseconds 750
        if ((Get-PageSource).Contains('com.android.systemui')) { break }
    }
    [void](Save-Probe 'notification-shade')
    Assert-Contains (Get-PageSource) 'com.android.systemui' 'Android notification shade did not open.'
}

function Close-NotificationShade {
    Invoke-Driver 'POST' '/back' @{} | Out-Null
    Start-Sleep -Milliseconds 500
}

function Find-FixtureInShade {
    Find-XPath "//*[contains(@text,'$script:fixtureTitle') or contains(@content-desc,'$script:fixtureTitle')]"
}

function Swipe-FixtureNotification {
    $element = Find-FixtureInShade
    $rect = (Invoke-Driver 'GET' "/element/$element/rect").value
    $window = (Invoke-Driver 'GET' '/window/rect').value
    $y = [int]($rect.y + ($rect.height / 2))
    # UiAutomator dragGesture starts with a long press and opens notification
    # channel controls. Use a short native swipe inside this reserved device.
    Invoke-Adb @('shell','input','swipe', [string][int]($window.width * 0.2), [string]$y,
        [string][int]($window.width * 0.98), [string]$y, '220') | Out-Null
    Start-Sleep -Milliseconds 700
}

function Wait-NotificationState([bool]$Present) {
    for ($attempt=0; $attempt -lt 20; $attempt++) {
        $record = Get-ActiveRecord
        $found = $null -ne $record
        if ($found -eq $Present) {
            if ($Present) { Assert-Contains $record $script:fixtureTitle 'Restored active notification did not contain the unique task.' }
            return
        }
        Start-Sleep -Milliseconds 500
    }
    throw "Active notification id 100 did not reach expected present=$Present state."
}

function Wait-FixtureInShade([bool]$Present) {
    for ($attempt=0; $attempt -lt 30; $attempt++) {
        $found = (Get-PageSource).Contains($script:fixtureTitle)
        if ($found -eq $Present) { return }
        Start-Sleep -Milliseconds 150
    }
    throw "Notification shade did not reach expected fixture-present=$Present state."
}

function Ensure-MainPage {
    for ($attempt=0; $attempt -lt 8; $attempt++) {
        $source = Get-PageSource
        if ($source.Contains('AddTodayTask')) { return }
        try { Invoke-Driver 'POST' '/back' @{} | Out-Null } catch { break }
        Start-Sleep -Milliseconds 350
    }
    if (-not ((Get-PageSource).Contains('AddTodayTask'))) { throw 'Could not return to Doing it Home.' }
}

function Open-NotificationSettings {
    Ensure-MainPage
    Click-ById 'Notificationsettings'
    [void](Find-ById 'DailyNotification')
    [void](Find-ById 'StickyNotification')
}

function Read-Switch([string]$Id) {
    $element = Find-ById $Id
    $value = Get-Attribute $element 'checked'
    if (-not $value) { $value = Get-Attribute $element 'value' }
    [pscustomobject]@{ Element=$element; On=($value -eq 'true') }
}

function Assert-OptionsOff {
    Open-NotificationSettings
    $daily = Read-Switch 'DailyNotification'
    $sticky = Read-Switch 'StickyNotification'
    if ($daily.On -or $sticky.On) {
        throw 'Daily notification test requires both notification options off at entry. Existing preference data was preserved.'
    }
}

function Set-DailyOn {
    $switch = Read-Switch 'DailyNotification'
    if (-not $switch.On) { Click-Element $switch.Element }
    Start-Sleep -Seconds 2
    # Android 13+ may show the runtime notification permission prompt.
    foreach ($choice in @('Allow','Allow notifications')) {
        $button = Try-FindByText $choice
        if ($button) { Click-Element $button; break }
    }
    Start-Sleep -Seconds 2
    # The app may open Android's exact-alarm settings when access is absent.
    # Back out without changing this pre-existing OS preference; inexact delivery remains supported.
    try {
        $source = Get-PageSource
        if (-not $source.Contains('DailyNotification')) { Invoke-Driver 'POST' '/back' @{} | Out-Null; Start-Sleep -Seconds 2 }
    } catch { }
    if (-not (Try-FindById 'DailyNotification')) {
        Ensure-MainPage
        Open-NotificationSettings
    }
    $daily = Read-Switch 'DailyNotification'
    Assert-True $daily.On 'Daily notification option did not remain enabled after permission handling.'
}

function Add-TodayFixture {
    Ensure-MainPage
    $homeSource = Get-PageSource
    $count = [regex]::Match($homeSource, '(\d+) tasks? on your mind today\.')
    if ($count.Success) { $script:priorRemainingCount = [int]$count.Groups[1].Value }
    else { $script:priorRemainingCount = 0 }
    if ($script:priorRemainingCount -ge 6) { throw 'This suite requires fewer than six remaining tasks so its fixture fits the notification preview. Existing tasks were preserved.' }
    Click-ById 'AddTodayTask'
    [void](Find-ById 'TaskTitle')
    $title = Find-ById 'TaskTitle'
    Invoke-Driver 'POST' "/element/$title/clear" @{} | Out-Null
    $characters = @($script:fixtureTitle.ToCharArray() | ForEach-Object { [string]$_ })
    Invoke-Driver 'POST' "/element/$title/value" @{ text=$script:fixtureTitle; value=$characters } | Out-Null
    Click-ById 'SaveTask'
    $script:fixtureCreated = $true
    [void](Find-ById 'AddTodayTask')
    Assert-Contains (Get-PageSource) $script:fixtureTitle 'New dated task did not appear on Home.'
}

function Get-ActiveNotificationDump {
    Invoke-Adb @('shell','dumpsys','notification','--noredact')
}

function Assert-FixtureNotification([int]$RemainingCount) {
    $record = Get-ActiveRecord
    Assert-True ($null -ne $record) 'Notification manager did not expose active com.doingit.app notification id 100.'
    $expectedTitle = if ($RemainingCount -eq 1) { '1 task left today' } else { "$RemainingCount tasks left today" }
    Assert-Contains $record $expectedTitle "Active notification did not contain title '$expectedTitle'."
    Assert-Contains $record "• $script:fixtureTitle" 'Active notification did not contain the unique dated task title.'
    Assert-Contains $record "$RemainingCount of " 'Active notification did not show the expected remaining task count.'
    $record
}

function Get-ActiveRecord {
    $dump = Get-ActiveNotificationDump
    [IO.File]::WriteAllText((Join-Path $script:outputDirectory 'notification-active.txt'), $dump, [Text.UTF8Encoding]::new($false))
    $section = [regex]::Match($dump, '(?is)^\s*(?:Notification List|Active Notifications):\s*(.*?)(?=^\s*(?:Historical Notification List|Historical Notifications|Ranking Config|Notification listeners):|\z)', [System.Text.RegularExpressions.RegexOptions]::Multiline)
    if (-not $section.Success) { throw 'Could not isolate the active notification section in dumpsys notification output.' }
    $record = [regex]::Matches($section.Groups[1].Value, '(?s)NotificationRecord\(.*?(?=\r?\n\s*NotificationRecord\(|\z)') |
        Where-Object { $_.Value.Contains('com.doingit.app') -and $_.Value -match 'id\s*=\s*100\b' } | Select-Object -First 1
    if ($null -eq $record) { return $null }
    $record.Value
}

function Assert-ChannelConfiguration {
    $dump = Get-ActiveNotificationDump
    $channel = [regex]::Matches($dump, '(?s)NotificationChannel\{.*?\}') |
        Where-Object { $_.Value.Contains('daily_tasks') } | Select-Object -First 1
    Assert-True ($null -ne $channel) 'Daily tasks notification channel was not present in notification manager state.'
    Assert-Contains $channel.Value 'mSound=null' 'Daily tasks channel was not silent.'
    # Android 16 stores an unspecified channel override even for channels created
    # as public; the actual notification's visibility is the effective request.
    Assert-Contains (Get-ActiveRecord) 'vis=PUBLIC' 'Daily summary did not request public lock-screen visibility.'
    Assert-True ($channel.Value -match 'mVibrationEnabled=false|mVibration=null') 'Daily tasks channel did not expose vibration as disabled.'
}

function Assert-NextMidnightAlarm([bool]$Expected) {
    $dump = Invoke-Adb @('shell','dumpsys','alarm')
    [IO.File]::WriteAllText((Join-Path $script:outputDirectory 'alarms.txt'), $dump, [Text.UTF8Encoding]::new($false))
    # Cancellation history also contains the action. Match only pending alarms.
    $alarm = [regex]::Match($dump, '(?m)^\s*RTC_WAKEUP #\d+: Alarm\{[^\r\n]*com\.doingit\.app\}\r?\n\s*tag=\*walarm\*:com\.doingit\.app\.DAILY_MIDNIGHT\r?\n\s*(?<details>type=[^\r\n]+)')
    Assert-True ($alarm.Success -eq $Expected) "Expected daily midnight alarm scheduled=$Expected; dumpsys alarm disagreed."
    if ($Expected) {
        $localDate = (Invoke-Adb @('shell','date','+%Y-%m-%d')).Trim()
        $tomorrow = [datetime]::ParseExact($localDate, 'yyyy-MM-dd', [cultureinfo]::InvariantCulture).AddDays(1).ToString('yyyy-MM-dd')
        Assert-Contains $alarm.Groups['details'].Value "origWhen=$tomorrow 00:00:00.000" 'Daily alarm did not target next local midnight.'
    }
}

function Verify-ExactAlarmAccess {
    $status = Get-Text (Find-ById 'NotificationStatus')
    if ($status.Contains('without it')) {
        $mode = Invoke-Adb @('shell','cmd','appops','get','com.doingit.app','SCHEDULE_EXACT_ALARM')
        $match = [regex]::Match($mode, 'SCHEDULE_EXACT_ALARM:\s*(\w+)')
        $script:exactAlarmMode = if ($match.Success) { $match.Groups[1].Value } else { 'default' }
        Click-ById 'NotificationTiming'
        [void](Save-Probe 'exact-alarm-settings')
        $toggle = Find-XPath '//*[@checkable="true"]'
        Assert-True ((Get-Attribute $toggle 'checked') -eq 'false') 'Alarm settings did not match the previously denied access.'
        Click-Element $toggle
        Invoke-Driver 'POST' '/back' @{} | Out-Null
        Start-Sleep -Seconds 2
    }
    Assert-Contains (Get-Text (Find-ById 'NotificationStatus')) 'On · Silent' 'Exact-alarm permission grant did not refresh the app status.'
    Assert-NextMidnightAlarm $true
    $dump = Invoke-Adb @('shell','dumpsys','alarm')
    Assert-True ($dump -match 'tag=\*walarm\*:com\.doingit\.app\.DAILY_MIDNIGHT\r?\n\s*type=RTC_WAKEUP[^\r\n]*window=0\b') 'Granted alarm access did not schedule an exact midnight wakeup.'
    [void](Save-Probe 'exact-alarm-enabled')
}

function Inspect-LockScreen {
    $policy = Invoke-Adb @('shell','dumpsys','window','policy')
    [IO.File]::WriteAllText((Join-Path $script:outputDirectory 'lock-policy.txt'), $policy)
    if ($policy -notmatch '(?i)secure=false') {
        Write-Host 'SKIP lock-screen interaction: device policy does not establish an unsecured keyguard; preserve its security settings.'
        return
    }
    # Only an unsecured device enters this branch. Preserve whether its swipe
    # keyguard was disabled; never set, change or remove a PIN/password.
    $disabled = (Invoke-Adb @('shell','locksettings','get-disabled')).Trim()
    Assert-True ($disabled -in @('true','false')) 'Could not establish the baseline unsecured lock-screen setting.'
    try {
        if ($disabled -eq 'true') { Invoke-Adb @('shell','locksettings','set-disabled','false') | Out-Null }
        Invoke-Adb @('shell','input','keyevent','223') | Out-Null
        Start-Sleep -Seconds 1
        Invoke-Adb @('shell','input','keyevent','224') | Out-Null
        Start-Sleep -Seconds 1
        $source = Save-Probe 'lock-screen'
        $lockedPolicy = Invoke-Adb @('shell','dumpsys','window','policy')
        [IO.File]::WriteAllText((Join-Path $script:outputDirectory 'locked-policy.txt'), $lockedPolicy)
        if ($lockedPolicy -match 'showing=true') {
            $remaining = $script:priorRemainingCount + 1
            $title = if ($remaining -eq 1) { '1 task left today' } else { "$remaining tasks left today" }
            Assert-Contains $source $title 'Lock screen did not show the enabled daily notification.'
            $expand = Find-XPath "//*[@text='$title']/ancestor::*[@resource-id='com.android.systemui:id/expandableNotificationRow'][1]//*[@resource-id='android:id/expand_button']"
            Click-Element $expand
            Assert-Contains (Save-Probe 'lock-screen-expanded') $script:fixtureTitle 'Expanded lock-screen notification did not show the remaining task.'
            Write-Host 'PASS daily notification visible on the unsecured lock screen.'
        } else { throw 'The unsecured keyguard did not appear after enabling the swipe lock.' }
    } finally {
        Invoke-Adb @('shell','input','keyevent','224') | Out-Null
        Invoke-Adb @('shell','input','keyevent','82') | Out-Null
        if ($disabled -eq 'true') { Invoke-Adb @('shell','locksettings','set-disabled','true') | Out-Null }
    }
}

function Open-NotificationPage {
    Open-NotificationSettings
    $switch = Read-Switch 'StickyNotification'
    if (-not $switch.On) { Click-Element $switch.Element }
    Start-Sleep -Seconds 2
    if (-not (Try-FindById 'StickyNotification')) { Open-NotificationSettings }
    Assert-True (Read-Switch 'StickyNotification').On 'Sticky notification option did not remain enabled.'
}

function Complete-Fixture {
    Ensure-MainPage
    $complete = Find-XPath "//*[@content-desc='Complete $script:fixtureTitle']"
    Click-Element $complete
    Start-Sleep -Seconds 2
    $completedRecord = Get-ActiveRecord
    Assert-True ($null -ne $completedRecord) 'Notification disappeared completely after completing a task.'
    if ($completedRecord) { Assert-NotContains $completedRecord $script:fixtureTitle 'Completed task remained in the active notification after task completion.' }
    if ($script:priorRemainingCount -eq 0) {
        Assert-Contains $completedRecord 'All done for today' 'Completing the only remaining task did not show the all-done summary.'
    } else {
        $expectedTitle = if ($script:priorRemainingCount -eq 1) { '1 task left today' } else { "$($script:priorRemainingCount) tasks left today" }
        Assert-Contains $completedRecord $expectedTitle 'Completing the fixture did not update the remaining count.'
        Assert-Contains $completedRecord "$($script:priorRemainingCount) of " 'Completed fixture count was wrong in the remaining summary.'
    }
    $completed = Find-XPath "//*[starts-with(@content-desc, 'View completed tasks in Today')]"
    Click-Element $completed
    Click-Element (Find-XPath "//*[@text='$script:fixtureTitle']/following::*[@text='Undo'][1]")
    Click-ById 'Closecompletedtasks'
    Start-Sleep -Seconds 2
    [void](Find-ById 'AddTodayTask')
    [void](Assert-FixtureNotification ($script:priorRemainingCount + 1))
}

function Delete-Fixture {
    Ensure-MainPage
    $source = Get-PageSource
    if (-not $source.Contains("Complete $script:fixtureTitle")) {
        $completedLink = Try-FindById 'CompletedTodaysTasks'
        if (-not $completedLink) { $completedLink = Find-XPath "//*[starts-with(@content-desc, 'View completed tasks in Today')]" }
        Click-Element $completedLink
        if (Try-FindByText $script:fixtureTitle) {
            Click-Element (Find-XPath "//*[@text='$script:fixtureTitle']/following::*[@text='Undo'][1]")
            Click-ById 'Closecompletedtasks'
            [void](Find-ById 'AddTodayTask')
            $source = Get-PageSource
        }
    }
    Assert-Contains $source "Complete $script:fixtureTitle" 'Could not find the test-owned dated task to remove it during cleanup.'
    Click-Element (Find-XPath "//*[@content-desc='Edit $script:fixtureTitle']")
    [void](Find-ById 'TaskTitle')
    Click-ByText 'Delete task'
    [void](Find-ByText 'Delete this task?')
    Click-ByText 'Delete'
    [void](Find-ById 'AddTodayTask')
}

$failure = $null
$cleanupFailure = $null
try {
    $sdk = [int](Invoke-Adb @('shell','getprop','ro.build.version.sdk')).Trim()
    $permissionState = Invoke-Adb @('shell','dumpsys','package','com.doingit.app')
    $permissionMatch = [regex]::Match($permissionState, 'android\.permission\.POST_NOTIFICATIONS:\s*granted=(true|false)')
    if ($sdk -ge 33 -and -not $permissionMatch.Success) { throw 'Could not establish the baseline POST_NOTIFICATIONS permission state; refusing to mutate Android permission state.' }
    if ($permissionMatch.Success) { $script:permissionInitiallyGranted = $permissionMatch.Groups[1].Value -eq 'true' }
    [void](Save-Probe '00-entry')
    Assert-OptionsOff
    $script:preconditionPassed = $true
    Ensure-MainPage
    Add-TodayFixture
    Open-NotificationSettings
    [void](Save-Probe '01-settings-before-enable')
    Set-DailyOn
    [void](Save-Probe '02-settings-enabled')
    $active = Assert-FixtureNotification ($script:priorRemainingCount + 1)
    Assert-ChannelConfiguration
    Assert-NextMidnightAlarm $true
    [void](Save-Probe '03-notification-asserted')
    Verify-ExactAlarmAccess

    Complete-Fixture
    [void](Save-Probe '04-task-completed-and-undo')

    Open-NotificationPage
    $sticky = Assert-FixtureNotification ($script:priorRemainingCount + 1)
    $flags = [regex]::Match($sticky, '(?m)^\s*(?:mFlags|flags)\s*=\s*0x([0-9a-fA-F]+)')
    $ongoing = if ($flags.Success) { ([Convert]::ToInt32($flags.Groups[1].Value,16) -band 2) -ne 0 } else { $sticky -match 'flags=[^\r\n]*ONGOING_EVENT' }
    Assert-True $ongoing 'Sticky notification did not set Notification.FLAG_ONGOING_EVENT (0x2).'
    [void](Save-Probe '05-sticky-enabled')
    Inspect-LockScreen

    Open-NotificationShade
    [void](Find-FixtureInShade)
    $beforeSwipe = [regex]::Match((Get-ActiveRecord), 'mCreationTimeMs=(\d+)').Groups[1].Value
    Swipe-FixtureNotification
    # Reposting can finish before Appium returns from the swipe. Do not require
    # observing a transient absence; both non-dismissal and restoration satisfy
    # the sticky contract. Normal-mode swipe below proves the gesture can dismiss.
    Wait-FixtureInShade $true
    Wait-NotificationState $true
    if ($sdk -ge 34) {
        $afterSwipe = [regex]::Match((Get-ActiveRecord), 'mCreationTimeMs=(\d+)').Groups[1].Value
        Assert-True ([long]$afterSwipe -gt [long]$beforeSwipe) 'Swipe did not produce a newly restored sticky notification.'
    }
    [void](Save-Probe '05b-sticky-swipe-restored')
    if (-not (Get-PageSource).Contains($script:fixtureTitle)) { Open-NotificationShade }
    Click-Element (Find-FixtureInShade)
    [void](Find-ById 'AddTodayTask')
    Assert-Contains (Get-PageSource) $script:fixtureTitle 'Tapping the notification did not open Doing it Home with its task intact.'
    [void](Save-Probe '05c-notification-tap-opens-home')

    Open-NotificationSettings
    $stickySwitch = Read-Switch 'StickyNotification'
    Click-Element $stickySwitch.Element
    Start-Sleep -Seconds 2
    Open-NotificationSettings
    Open-NotificationShade
    Swipe-FixtureNotification
    Wait-NotificationState $false
    Close-NotificationShade
    Ensure-MainPage
    Click-Element (Find-XPath "//*[@content-desc='Edit $script:fixtureTitle']")
    [void](Find-ById 'TaskTitle')
    Click-ByText 'Save changes'
    [void](Find-ById 'AddTodayTask')
    Wait-NotificationState $false
    [void](Save-Probe '05d-normal-dismissal-persists-after-task-refresh')
    Open-NotificationSettings
    $dailySwitch = Read-Switch 'DailyNotification'
    Click-Element $dailySwitch.Element
    Start-Sleep -Seconds 2
    Assert-NextMidnightAlarm $false
    Assert-True ($null -eq (Get-ActiveRecord)) 'Disabling daily notifications left active notification id 100.'
    [void](Save-Probe '06-disabled')
    Delete-Fixture
    $script:fixtureCreated = $false
    Write-Host "PASS daily notification summary, task completion/Undo, sticky flag and disable cleanup on $($context.AvdName) ($script:serial); fixture $script:fixtureTitle."
} catch {
    $failure = $_
    Write-Host $_.ScriptStackTrace
    try { [void](Save-Probe 'failure') } catch { Write-Warning "Could not save Appium failure probe: $($_.Exception.Message)" }
    try {
        $dump = Get-ActiveNotificationDump
        [IO.File]::WriteAllText((Join-Path $script:outputDirectory 'failure-notification.txt'), $dump, [Text.UTF8Encoding]::new($false))
    } catch { }
} finally {
    try {
        if ($script:preconditionPassed) {
            Ensure-MainPage
            Open-NotificationSettings
            $stickySwitch = Read-Switch 'StickyNotification'
            if ($stickySwitch.On) { Click-Element $stickySwitch.Element; Start-Sleep -Seconds 1; Open-NotificationSettings }
            $dailySwitch = Read-Switch 'DailyNotification'
            if ($dailySwitch.On) { Click-Element $dailySwitch.Element; Start-Sleep -Seconds 2 }
        }
        if ($script:fixtureCreated) { Delete-Fixture }
        if ($null -ne $script:exactAlarmMode) {
            Invoke-Adb @('shell','cmd','appops','set','com.doingit.app','SCHEDULE_EXACT_ALARM',$script:exactAlarmMode) | Out-Null
        }
        if (-not $script:permissionInitiallyGranted) {
            $current = Invoke-Adb @('shell','dumpsys','package','com.doingit.app')
            if ($current -match 'android\.permission\.POST_NOTIFICATIONS:\s*granted=true') {
                Invoke-Adb @('shell','pm','revoke','com.doingit.app','android.permission.POST_NOTIFICATIONS') | Out-Null
            }
        }
        [void](Save-Probe '07-after-cleanup')
    } catch { $cleanupFailure = $_ }
}

if ($failure) {
    if ($cleanupFailure) { throw "Suite failed: $($failure.Exception.Message) Cleanup also failed: $($cleanupFailure.Exception.Message)" }
    throw $failure
}
if ($cleanupFailure) { throw $cleanupFailure }
