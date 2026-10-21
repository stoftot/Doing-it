param([Parameter(Mandatory=$true)][string]$ContextPath,[hashtable]$TestParameters=@{})

$ErrorActionPreference = 'Stop'
$context = Get-Content -LiteralPath $ContextPath -Raw | ConvertFrom-Json
if (-not $context.SessionUrl) { throw 'TaskDetails requires the runner-owned Managed Appium session.' }
$script:sessionUrl = [string]$context.SessionUrl
$script:outputDirectory = [string]$context.OutputDirectory
$script:createdTasks = [System.Collections.Generic.List[object]]::new()
$script:createdRoutines = [System.Collections.Generic.List[string]]::new()
$runId = [guid]::NewGuid().ToString('N').Substring(0, 10)
$listTitle = "QA-DETAILS-$runId"
$dateTitle = "QA-DATE-$runId"
$routineTitle = "QA-ROUTINE-$runId"
$checklistTitle = "QA-CHECK-$runId"
$detailsToken = "QA-DESC-$runId"
$routineToken = "QA-RECURRING-DESC-$runId"
$longDescription = "$detailsToken. " + ("Keep this optional detail attached to its task. " * 30) + "`n`nA second paragraph checks that line breaks and paragraph spacing survive saving, editing, and reopening."
$routineDescription = "$routineToken. " + ("This recurring description remains available from its scheduled occurrence. " * 6) + "`n`nThe second paragraph stays readable."
$invalidRenderedTitle = 'Repeated words that should exceed the available task row width ' * 3

function Invoke-Driver([string]$Method,[string]$Path,$Body=$null) {
    $request = @{ Uri = "$script:sessionUrl$Path"; Method = $Method; TimeoutSec = 60 }
    if ($null -ne $Body) {
        $request.ContentType = 'application/json; charset=utf-8'
        $request.Body = ConvertTo-Json -InputObject $Body -Depth 20 -Compress
    }
    try { Invoke-RestMethod @request }
    catch { throw "$Method $Path $($request.Body): $($_.Exception.Message)" }
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
    $escaped = $Id.Replace("'", "&apos;")
    $xpath = "//*[contains(@resource-id, ':id/$escaped') or @resource-id='$escaped' or @content-desc='$escaped' or @name='$escaped']"
    Find-XPath $xpath
}

function Find-ByText([string]$Text) {
    $literal = if (-not $Text.Contains("'")) { "'$Text'" } elseif (-not $Text.Contains('"')) { '"' + $Text + '"' } else { throw "XPath text containing both quote types is unsupported: $Text" }
    Find-XPath "//*[@text=$literal or @content-desc=$literal or @name=$literal]"
}

function Set-ImplicitWait([int]$Milliseconds) {
    Invoke-Driver 'POST' '/timeouts' @{ implicit = $Milliseconds } | Out-Null
}

function Find-ScrollableText([string]$Text) {
    Set-ImplicitWait 1200
    try {
        for ($attempt = 0; $attempt -lt 8; $attempt++) {
            try { return Find-ByText $Text } catch { }
            $scroll = Find-XPath '//android.widget.ScrollView[@scrollable="true"]'
            Invoke-Driver 'POST' '/execute/sync' @{ script = 'mobile: scrollGesture'; args = @(@{ elementId = $scroll; direction = 'down'; percent = 0.75 }) } | Out-Null
            Start-Sleep -Milliseconds 300
        }
    } finally { Set-ImplicitWait 15000 }
    throw "Could not bring '$Text' into the visible app area after scrolling."
}

function Try-FindById([string]$Id) {
    try { Find-ById $Id } catch { $null }
}

function Try-FindByText([string]$Text) {
    try { Find-ByText $Text } catch { $null }
}

function Click-Element([string]$Id) {
    Invoke-Driver 'POST' "/element/$Id/click" @{} | Out-Null
    Start-Sleep -Milliseconds 300
}

function Click-ById([string]$Id) { Click-Element (Find-ById $Id) }
function Click-ByText([string]$Text) { Click-Element (Find-ByText $Text) }

function Get-Text([string]$Id) {
    [string](Invoke-Driver 'GET' "/element/$Id/text").value
}

function Get-Attribute([string]$Id,[string]$Name) {
    [string](Invoke-Driver 'GET' "/element/$Id/attribute/$Name").value
}

function Replace-InputText([string]$Id,[string]$Text) {
    $element = Find-ById $Id
    Invoke-Driver 'POST' "/element/$element/clear" @{} | Out-Null
    $characters = @($Text.ToCharArray() | ForEach-Object { [string]$_ })
    Invoke-Driver 'POST' "/element/$element/value" @{ text = $Text; value = $characters } | Out-Null
    $element
}

function Type-SubtaskAndNext([string]$Text) {
    $input = Find-XPath "(//android.widget.EditText[starts-with(@resource-id, 'com.doingit.app:id/SubtaskTitle')])[last()]"
    Click-Element $input
    Invoke-Driver 'POST' "/element/$input/value" @{ text = $Text; value = @($Text.ToCharArray() | ForEach-Object { [string]$_ }) } | Out-Null
    Invoke-Driver 'POST' '/execute/sync' @{ script = 'mobile: performEditorAction'; args = @(@{ action = 'next' }) } | Out-Null
    Start-Sleep -Milliseconds 300
}

function Set-LastSubtaskText([string]$Text) {
    $input = Find-XPath "(//android.widget.EditText[starts-with(@resource-id, 'com.doingit.app:id/SubtaskTitle')])[last()]"
    Invoke-Driver 'POST' "/element/$input/clear" @{} | Out-Null
    Invoke-Driver 'POST' "/element/$input/value" @{ text = $Text; value = @($Text.ToCharArray() | ForEach-Object { [string]$_ }) } | Out-Null
}

function Set-FirstSubtaskText([string]$Text) {
    $input = Find-XPath "(//android.widget.EditText[starts-with(@resource-id, 'com.doingit.app:id/SubtaskTitle')])[1]"
    Invoke-Driver 'POST' "/element/$input/clear" @{} | Out-Null
    Invoke-Driver 'POST' "/element/$input/value" @{ text = $Text; value = @($Text.ToCharArray() | ForEach-Object { [string]$_ }) } | Out-Null
}

function Checklist-Progress([string]$Expected,[string]$Message) {
    $actual = Get-Text (Find-ById 'ChecklistProgress')
    Assert-True ($actual -ceq $Expected) "$Message Expected '$Expected', got '$actual'."
}

function Get-PageSource {
    [string](Invoke-Driver 'GET' '/source').value
}

function Save-Probe([string]$Name) {
    $source = Get-PageSource
    [IO.File]::WriteAllText((Join-Path $script:outputDirectory "$Name.xml"), $source, [Text.UTF8Encoding]::new($false))
    $screenshot = Invoke-Driver 'GET' '/screenshot'
    [IO.File]::WriteAllBytes((Join-Path $script:outputDirectory "$Name.png"), [Convert]::FromBase64String([string]$screenshot.value))
    $source
}

function Assert-True([bool]$Condition,[string]$Message) {
    if (-not $Condition) { throw $Message }
}

function Assert-Contains([string]$Text,[string]$Needle,[string]$Message) {
    if (-not $Text.Contains($Needle,[StringComparison]::Ordinal)) { throw $Message }
}

function Assert-NotContains([string]$Text,[string]$Needle,[string]$Message) {
    if ($Text.Contains($Needle,[StringComparison]::Ordinal)) { throw $Message }
}

function Dismiss-ValidationAlert([string]$ExpectedMessage) {
    [void](Find-ByText "Couldn't save")
    $source = Get-PageSource
    Assert-Contains $source "Couldn't save" 'Expected the editor to reject this invalid title with a save validation alert.'
    Assert-Contains $source $ExpectedMessage "Save validation alert did not include the expected message '$ExpectedMessage'."
    Click-ByText 'OK'
}

function Accept-DeleteConfirmation {
    [void](Find-ByText 'Delete this task?')
    $source = Get-PageSource
    Assert-Contains $source 'Delete this task?' 'Expected the app to ask for confirmation before deleting a test-owned fixture.'
    Click-ByText 'Delete'
}

function Find-NodeBounds([string]$Source,[string]$NodeName) {
    [xml]$document = $Source
    foreach ($node in $document.SelectNodes('//*[@resource-id or @content-desc or @name]')) {
        foreach ($attribute in @('resource-id','content-desc','name')) {
            if ($node.HasAttribute($attribute) -and $node.GetAttribute($attribute).Contains($NodeName) -and $node.HasAttribute('bounds')) {
                $match = [regex]::Match($node.GetAttribute('bounds'), '^\[(\d+),(\d+)\]\[(\d+),(\d+)\]$')
                if ($match.Success) {
                    return [pscustomobject]@{
                        Left = [int]$match.Groups[1].Value; Top = [int]$match.Groups[2].Value
                        Right = [int]$match.Groups[3].Value; Bottom = [int]$match.Groups[4].Value
                    }
                }
            }
        }
    }
    $null
}

function Open-Details([string]$Title) {
    Click-Element (Find-ScrollableText $Title)
    [void](Find-ById 'TaskDetailsTitle')
    $shown = Get-Text (Find-ById 'TaskDetailsTitle')
    Assert-Contains $shown $Title "Task details title did not match '$Title'."
}

function Close-Details {
    $close = Try-FindById 'Closetaskdetails'
    if (-not $close) { $close = Find-XPath "//*[@content-desc='Close task details']" }
    Click-Element $close
}

function Assert-OverlayBounds([string]$Source) {
    $card = Find-NodeBounds $Source 'TaskDetailsCard'
    if (-not $card) {
        throw 'Appium page source did not expose TaskDetailsCard bounds; popup margin could not be asserted.'
    }
    $window = (Invoke-Driver 'GET' '/window/rect').value
    $width = [int]$window.width; $height = [int]$window.height
    Assert-True ($card.Left -gt 0 -and $card.Top -gt 0 -and $card.Right -lt $width -and $card.Bottom -lt $height) `
        "Task details card did not leave a visible margin inside the app window: card=[$($card.Left),$($card.Top)][$($card.Right),$($card.Bottom)], window=${width}x${height}."
    [xml]$document = $Source
    $node = $document.SelectSingleNode('//*[@resource-id="com.doingit.app:id/TaskDetailsCard"]')
    Assert-True ($null -ne $node) 'Details card was missing from native hierarchy.'
    Assert-True ($node.SelectNodes('.//android.widget.EditText | .//android.widget.CheckBox | .//android.widget.Switch').Count -eq 0) 'Task details contained editable controls.'
}

function Assert-DescriptionScrolls {
    $description = Find-ById 'TaskDetailsDescription'
    $before = (Invoke-Driver 'GET' "/element/$description/rect").value
    $scroll = Find-XPath '//android.widget.ScrollView[@scrollable="true"]'
    $moved = Invoke-Driver 'POST' '/execute/sync' @{ script = 'mobile: scrollGesture'; args = @(@{ elementId=$scroll; direction='down'; percent=0.8 }) }
    Start-Sleep -Milliseconds 300
    $after = (Invoke-Driver 'GET' "/element/$description/rect").value
    Assert-True ($after.y -lt $before.y) 'The long description did not scroll within its popup.'
    for ($attempt=0; $attempt -lt 10 -and $moved.value; $attempt++) {
        $moved = Invoke-Driver 'POST' '/execute/sync' @{ script='mobile: scrollGesture'; args=@(@{elementId=$scroll; direction='down'; percent=0.8}) }
    }
    Assert-True (-not $moved.value) 'Could not reach the end of the long description.'
    Save-Probe '02b-long-description-scrolled-to-end' | Out-Null
}

function Tap-OutsideCard {
    $source = Get-PageSource
    $backdrop = Find-NodeBounds $source 'TaskDetailsBackdrop'
    $card = Find-NodeBounds $source 'TaskDetailsCard'
    if (-not $backdrop -or -not $card) { throw 'Appium page source did not expose backdrop/card bounds for a safe outside-tap assertion.' }
    $window = (Invoke-Driver 'GET' '/window/rect').value
    $x = $backdrop.Left + 2
    $y = [int](($backdrop.Top + $backdrop.Bottom) / 2)
    Assert-True ($x -ge [int]$window.x -and $x -lt ([int]$window.x + [int]$window.width) -and $y -ge [int]$window.y -and $y -lt ([int]$window.y + [int]$window.height)) 'Outside tap point was not inside the app window.'
    Assert-True (-not ($x -ge $card.Left -and $x -le $card.Right -and $y -ge $card.Top -and $y -le $card.Bottom)) 'Outside tap point overlapped the details card.'
    Invoke-Driver 'POST' '/execute/sync' @{ script = 'mobile: clickGesture'; args = @(@{ x = $x; y = $y }) } | Out-Null
}

function Assert-NoDetailsOverlay {
    for ($attempt = 0; $attempt -lt 12; $attempt++) {
        $source = Get-PageSource
        if (-not $source.Contains('TaskDetailsOverlay')) { return }
        Start-Sleep -Milliseconds 250
    }
    throw 'Task details overlay remained after its dismissal action.'
}

function Ensure-MainPage {
    for ($attempt = 0; $attempt -lt 8; $attempt++) {
        $source = Get-PageSource
        if (-not $source.Contains('TaskDetailsOverlay') -and -not $source.Contains('TaskTitle') -and
            ($source.Contains('TabHome') -or $source.Contains('TabLists') -or $source.Contains('TabRecurring'))) { return }
        try { Invoke-Driver 'POST' '/back' @{} | Out-Null } catch { break }
        Start-Sleep -Milliseconds 300
    }
    $source = Get-PageSource
    if (-not ($source.Contains('TabHome') -or $source.Contains('TabLists') -or $source.Contains('TabRecurring'))) {
        throw 'Could not return to the app main page for fixture cleanup.'
    }
}

function Open-TaskEditor([string]$Title) {
    [void](Find-ScrollableText $Title)
    $xpath = "//*[@content-desc='Edit $Title' or contains(@resource-id, ':id/Edit$Title') or @resource-id='com.doingit.app:id/Edit$Title']"
    Click-Element (Find-XPath $xpath)
    [void](Find-ById 'TaskTitle')
}

function Delete-TaskFixture($Fixture) {
    Ensure-MainPage
    if ($Fixture.Kind -eq 'date') { Click-ById 'TabHome' } else { Click-ById 'TabLists' }
    if ($Fixture.Kind -eq 'checklist') {
        [void](Find-ScrollableText $Fixture.Title)
        Click-Element (Find-XPath "//*[@content-desc='Open $($Fixture.Title)']")
        Click-ById 'EditChecklist'
    } else { Open-TaskEditor $Fixture.Title }
    [void](Find-ScrollableText 'Delete task')
    Click-ByText 'Delete task'
    Accept-DeleteConfirmation
    if ($Fixture.Kind -eq 'checklist') {
        [void](Find-ByText 'This task was deleted.')
        Click-ByText 'Close'
    }
    [void](Find-ById 'TabLists')
    Assert-NotContains (Get-PageSource) $Fixture.Title 'Deleted test-owned task is still displayed.'
}

function Delete-RoutineFixture([string]$Title) {
    Ensure-MainPage
    Click-ById 'TabRecurring'
    [void](Find-ScrollableText $Title)
    $xpath = "//*[@text='$Title']/following::*[@text='Edit routine'][1]"
    Click-Element (Find-XPath $xpath)
    [void](Find-ById 'TaskTitle')
    [void](Find-ScrollableText 'Delete routine')
    Click-ByText 'Delete routine'
    Accept-DeleteConfirmation
    [void](Find-ById 'TabRecurring')
}

function Save-NewTask([string]$Title,[string]$Description,[bool]$Dated) {
    if ($Dated) { Click-ById 'AddTodayTask' } else { Click-ById 'CreateTask' }
    [void](Find-ById 'TaskTitle')
    if (-not $Dated) {
        Click-ByText 'Add task'
        Dismiss-ValidationAlert 'Task title is required.'
        Replace-InputText 'TaskTitle' $invalidRenderedTitle | Out-Null
        Click-ByText 'Add task'
        Dismiss-ValidationAlert 'two lines'
    }
    Replace-InputText 'TaskTitle' $Title | Out-Null
    if ($Description) { Replace-InputText 'TaskDescription' $Description | Out-Null }
    $script:createdTasks.Add([pscustomobject]@{ Title = $Title; Kind = $(if ($Dated) { 'date' } else { 'list' }) })
    Click-ByText 'Add task'
    [void](Find-ById 'TabLists')
    [void](Find-ScrollableText $Title)
}

function Save-NewRoutine([string]$Title,[string]$Description) {
    Click-ById 'TabRecurring'
    Click-ById 'CreateRecurringTask'
    [void](Find-ById 'TaskTitle')
    Replace-InputText 'TaskTitle' $Title | Out-Null
    Replace-InputText 'TaskDescription' $Description | Out-Null
    $script:createdRoutines.Add($Title)
    Click-ByText 'Create routine'
    [void](Find-ById 'TabRecurring')
    [void](Find-ScrollableText $Title)
}

Invoke-Driver 'POST' '/timeouts' @{ implicit = 15000 } | Out-Null
$failure = $null
$cleanupFailure = $null
try {
    $longDescription = $longDescription.TrimEnd()
    $routineDescription = $routineDescription.TrimEnd()
    Assert-True ($longDescription.Length -gt 250) 'The task details fixture must exceed 250 characters.'
    Assert-True ($routineDescription.Length -gt 250) 'The recurring details fixture must exceed 250 characters.'

    [void](Find-ById 'TabHome')
    Ensure-MainPage
    Click-ById 'TabLists'
    Save-NewTask $listTitle $longDescription $false
    $rowSource = Save-Probe '01-list-row-no-description'
    Assert-NotContains $rowSource $detailsToken 'List task row exposed its optional description before opening details.'

    Open-Details $listTitle
    $detailsSource = Save-Probe '02-list-details-open'
    $descriptionId = Find-ById 'TaskDetailsDescription'
    $shownDescription = Get-Text $descriptionId
    Assert-Contains $shownDescription $detailsToken 'Long description was not visible in task details.'
    Assert-Contains $shownDescription 'A second paragraph checks' 'Multiline description lost its second paragraph in task details.'
    Assert-OverlayBounds $detailsSource
    Assert-DescriptionScrolls
    Click-Element $descriptionId
    [void](Find-ById 'TaskDetailsTitle')
    Save-Probe '03-inside-tap-keeps-details-open' | Out-Null
    Close-Details
    Assert-NoDetailsOverlay

    Open-Details $listTitle
    Close-Details
    Open-Details $listTitle
    Tap-OutsideCard
    Assert-NoDetailsOverlay
    Save-Probe '04-outside-tap-dismisses-details' | Out-Null

    Open-Details $listTitle
    Invoke-Driver 'POST' '/back' @{} | Out-Null
    Assert-NoDetailsOverlay
    Save-Probe '05-back-dismisses-details' | Out-Null

    Open-TaskEditor $listTitle
    $descriptionInput = Find-ById 'TaskDescription'
    $retained = Get-Attribute $descriptionInput 'text'
    if (-not $retained) { $retained = Get-Text $descriptionInput }
    Assert-True ($retained.TrimEnd() -ceq $longDescription.TrimEnd()) 'Editing the task did not retain its full multiline details.'
    Click-ByText 'Save changes'
    [void](Find-ByText $listTitle)
    Open-TaskEditor $listTitle
    Replace-InputText 'TaskDescription' '' | Out-Null
    Click-ByText 'Save changes'
    [void](Find-ByText $listTitle)
    Open-Details $listTitle
    $clearedSource = Save-Probe '06-cleared-details'
    Assert-NotContains $clearedSource 'TaskDetailsDescription' 'Clearing optional details left a description element in the read-only popup.'
    Assert-NotContains $clearedSource $detailsToken 'Cleared optional details remained visible.'
    Close-Details

    Click-Element (Find-XPath "//*[@content-desc='Complete $listTitle']")
    Click-ById 'CompletedGeneral'
    Open-Details $listTitle
    Assert-OverlayBounds (Save-Probe '06b-completed-task-details')
    Close-Details
    Click-Element (Find-XPath "//*[@text='$listTitle']/following::*[@text='Undo'][1]")
    Click-ById 'Closecompletedtasks'
    [void](Find-ByText $listTitle)

    Click-ById 'TabHome'
    Save-NewTask $dateTitle '' $true
    $homeSource = Save-Probe '07-date-task-row'
    Assert-Contains $homeSource $dateTitle 'New dated task did not appear on Home.'
    Click-ById 'Opencalendar'
    [void](Find-ByText $dateTitle)
    Open-Details $dateTitle
    $calendarSource = Save-Probe '08-calendar-task-details'
    $calendarLocation = Get-Text (Find-ById 'TaskDetailsLocation')
    Assert-Contains $calendarLocation ([string](Get-Date).Year) 'Calendar task details did not show its scheduled date context.'
    Assert-OverlayBounds $calendarSource
    Close-Details
    Assert-NoDetailsOverlay
    Invoke-Driver 'POST' '/back' @{} | Out-Null

    Save-NewRoutine $routineTitle $routineDescription
    $routineRows = Save-Probe '09-recurring-row-no-description'
    Assert-NotContains $routineRows $routineToken 'Recurring task row exposed its optional description before opening details.'
    Open-Details $routineTitle
    $routineSource = Save-Probe '10-recurring-details-open'
    $routineShown = Get-Text (Find-ById 'TaskDetailsDescription')
    Assert-Contains $routineShown $routineToken 'Recurring task details omitted its description.'
    $schedule = Get-Text (Find-ById 'TaskDetailsLocation')
    Assert-Contains $schedule 'Every day' 'Recurring task details did not include its daily schedule.'
    Assert-OverlayBounds $routineSource
    Close-Details

    Click-ById 'TabHome'
    Open-Details $routineTitle
    Assert-Contains (Get-Text (Find-ById 'TaskDetailsLocation')) 'Every day' 'A routine occurrence showed a destination instead of its schedule.'
    Close-Details

    Click-ById 'TabLists'
    Click-ById 'CreateTask'
    Replace-InputText 'TaskTitle' $checklistTitle | Out-Null
    Click-ById 'ChecklistTaskType'
    Type-SubtaskAndNext 'Book ticket'
    Type-SubtaskAndNext 'Pack bag'
    Type-SubtaskAndNext 'Check weather'
    Save-Probe '10c-checklist-editor-three-items-and-blank-draft' | Out-Null
    $script:createdTasks.Add([pscustomobject]@{Title=$checklistTitle; Kind='checklist'})
    Click-ById 'SaveTask'
    [void](Find-ById 'TabLists')
    Open-Details $checklistTitle
    Assert-OverlayBounds (Save-Probe '10b-checklist-read-only-details')
    Close-Details
    Click-Element (Find-XPath "//*[@content-desc='Open $checklistTitle']")
    Save-Probe '10d-checklist-open' | Out-Null
    Checklist-Progress '0 of 3 done' 'Opening checklist details changed the expected checklist size or broke the separate checklist control.'
    $secondItem = Find-ByText 'Pack bag'
    Click-Element $secondItem
    Checklist-Progress '1 of 3 done' 'Tapping a subtask label did not update checklist progress.'
    Click-Element (Find-XPath "//*[@content-desc='Complete Book ticket']")
    Checklist-Progress '2 of 3 done' 'Tapping a checkbox did not update checklist progress.'
    $thirdCheck = Find-XPath "//*[@content-desc='Complete Check weather']"
    Click-Element $thirdCheck
    Checklist-Progress '3 of 3 done' 'Completing the final subtask did not update checklist progress.'
    Assert-Contains (Get-PageSource) 'All done' 'A fully completed checklist did not show its all-done hint.'
    Click-ById 'CloseChecklist'
    Click-ById 'CompletedGeneral'
    Click-Element (Find-XPath "//*[@content-desc='Open $checklistTitle']")
    Checklist-Progress '3 of 3 done' 'Reopening the checklist lost its completed state.'
    $thirdCheck = Find-XPath "//*[@content-desc='Reopen Check weather']"
    Click-Element $thirdCheck
    Checklist-Progress '2 of 3 done' 'Unchecking one item did not reduce progress and reopen the task.'
    Click-ById 'EditChecklist'
    Replace-InputText 'TaskTitle' "$checklistTitle-READY" | Out-Null
    Set-FirstSubtaskText 'Reserve ticket'
    [void](Find-ScrollableText '+  Add item')
    Click-ByText '+  Add item'
    Set-LastSubtaskText 'Cancel this draft'
    Click-Element (Find-XPath "//*[@content-desc='Close']")
    Click-ById 'CloseChecklist'
    Click-Element (Find-XPath "//*[@content-desc='Open $checklistTitle']")
    Assert-Contains (Get-PageSource) $checklistTitle 'Canceling checklist edits saved an unsaved task title.'
    Assert-NotContains (Get-PageSource) "$checklistTitle-READY" 'Canceling checklist edits saved an unsaved task title.'
    Assert-Contains (Get-PageSource) 'Book ticket' 'Canceling checklist item edits changed the saved item title.'
    Assert-NotContains (Get-PageSource) 'Cancel this draft' 'Canceling checklist item edits saved an unsaved item.'
    Checklist-Progress '2 of 3 done' 'Canceling checklist item edits changed saved progress.'
    Click-ById 'EditChecklist'
    Replace-InputText 'TaskTitle' "$checklistTitle-READY" | Out-Null
    Set-FirstSubtaskText 'Reserve ticket'
    Click-Element (Find-XPath "//*[@content-desc='Remove item Pack bag']")
    [void](Find-ScrollableText '+  Add item')
    Click-ByText '+  Add item'
    Set-LastSubtaskText 'Share itinerary'
    Click-ByText 'Save changes'
    [void](Find-ById 'ChecklistProgress')
    $checklistTitle = "$checklistTitle-READY"
    $script:createdTasks[$script:createdTasks.Count - 1].Title = $checklistTitle
    Assert-Contains (Get-PageSource) $checklistTitle 'Saving the checklist edit did not preserve the task title change.'
    Assert-Contains (Get-PageSource) 'Reserve ticket' 'Saving an item rename did not update its title.'
    Assert-Contains (Get-PageSource) 'Share itinerary' 'Saving a new checklist item did not update its membership.'
    Assert-NotContains (Get-PageSource) 'Pack bag' 'Removing a checklist item did not update its membership.'
    Checklist-Progress '1 of 3 done' 'Saving checklist edits did not preserve matching item progress.'
    Click-ById 'EditChecklist'
    for ($item = 1; $item -le 9; $item++) {
        [void](Find-ScrollableText '+  Add item')
        Click-ByText '+  Add item'
        Set-LastSubtaskText "Extra item $item"
    }
    Click-ByText 'Save changes'
    [void](Find-ById 'ChecklistProgress')
    $scroll = Find-ById 'ChecklistScroll'
    Invoke-Driver 'POST' '/execute/sync' @{ script = 'mobile: scrollGesture'; args = @(@{ elementId = $scroll; direction = 'down'; percent = 0.8 }) } | Out-Null
    $lastLabel = Find-ScrollableText 'Extra item 9'
    $beforeRect = (Invoke-Driver 'GET' "/element/$lastLabel/rect").value
    Click-Element $lastLabel
    $sameLastLabel = Find-XPath "//*[@text='Extra item 9']"
    $afterRect = (Invoke-Driver 'GET' "/element/$sameLastLabel/rect").value
    Assert-True ([int]$beforeRect.y -eq [int]$afterRect.y) 'Toggling a visible item in a long checklist moved the scroll position.'
    Assert-True ($null -ne (Try-FindById 'ChecklistScroll')) 'The checklist scroll container disappeared after toggling a visible item.'
    $scroll = Find-ById 'ChecklistScroll'
    for ($attempt = 0; $attempt -lt 8; $attempt++) {
        $moved = Invoke-Driver 'POST' '/execute/sync' @{ script = 'mobile: scrollGesture'; args = @(@{ elementId = $scroll; direction = 'up'; percent = 0.9 }) }
        if (-not $moved.value) { break }
    }
    Checklist-Progress '2 of 12 done' 'Toggling a lower checklist item did not preserve accurate progress.'
    Click-ById 'CloseChecklist'

    Write-Host "PASS task details and overlay assertions on $($context.AvdName) ($($context.Serial)); fixtures $runId."
} catch {
    $failure = $_
    Write-Host $_.ScriptStackTrace
    try { Save-Probe 'failure' | Out-Null } catch { Write-Warning "Could not capture suite failure snapshot: $($_.Exception.Message)" }
} finally {
    try {
        Ensure-MainPage
        foreach ($fixture in @($script:createdTasks)) {
            try { Delete-TaskFixture $fixture } catch { throw "Could not remove test-owned task '$($fixture.Title)': $($_.Exception.Message)" }
        }
        foreach ($title in @($script:createdRoutines)) {
            try { Delete-RoutineFixture $title } catch { throw "Could not remove test-owned routine '$title': $($_.Exception.Message)" }
        }
        Save-Probe '11-after-cleanup' | Out-Null
    } catch { $cleanupFailure = $_ }
}

if ($failure) {
    if ($cleanupFailure) { throw "Suite failed: $($failure.Exception.Message) Cleanup also failed: $($cleanupFailure.Exception.Message)" }
    throw $failure
}
if ($cleanupFailure) { throw $cleanupFailure }
