[CmdletBinding()]
param(
    [string]$ApiBaseUrl = "http://localhost:8080/api",
    [string]$DemoPassword = "TeamPulseDemo2026!",
    [string]$WorkbookPath = (Join-Path $PSScriptRoot "..\database\seed\TeamPulse-Demo-Tasks.xlsx")
)

$ErrorActionPreference = "Stop"
$api = $ApiBaseUrl.TrimEnd("/")
$workbook = (Resolve-Path -LiteralPath $WorkbookPath).Path

$people = @(
    @{ Email = "bao.trung@teampulse.demo"; DisplayName = "Bao Trung"; Role = "Leader" },
    @{ Email = "minh.anh@teampulse.demo"; DisplayName = "Minh Anh"; Role = "Member" },
    @{ Email = "gia.huy@teampulse.demo"; DisplayName = "Gia Huy"; Role = "Member" },
    @{ Email = "thu.ha@teampulse.demo"; DisplayName = "Thu Ha"; Role = "Member" }
)

function Invoke-JsonRequest {
    param(
        [Parameter(Mandatory)] [string]$Method,
        [Parameter(Mandatory)] [string]$Path,
        [string]$Token,
        [object]$Body
    )

    $headers = @{ Accept = "application/json" }
    if ($Token) {
        $headers.Authorization = "Bearer $Token"
    }

    $arguments = @{
        Method = $Method
        Uri = "$api$Path"
        Headers = $headers
    }
    if ($PSBoundParameters.ContainsKey("Body")) {
        $arguments.ContentType = "application/json"
        $arguments.Body = $Body | ConvertTo-Json -Depth 8
    }

    try {
        Invoke-RestMethod @arguments
    }
    catch {
        $detail = $_.ErrorDetails.Message
        if (-not $detail) { $detail = $_.Exception.Message }
        throw "$Method $Path failed: $detail"
    }
}

function Get-Session {
    param([hashtable]$Person)

    try {
        Invoke-JsonRequest -Method Post -Path "/auth/login" -Body @{
            email = $Person.Email
            password = $DemoPassword
        }
    }
    catch {
        Invoke-JsonRequest -Method Post -Path "/auth/register" -Body @{
            email = $Person.Email
            displayName = $Person.DisplayName
            password = $DemoPassword
        }
    }
}

function Send-Workbook {
    param(
        [string]$Path,
        [string]$Token,
        [string]$FilePath
    )

    $client = [System.Net.Http.HttpClient]::new()
    $client.DefaultRequestHeaders.Authorization =
        [System.Net.Http.Headers.AuthenticationHeaderValue]::new("Bearer", $Token)
    $content = [System.Net.Http.MultipartFormDataContent]::new()
    $stream = [System.IO.File]::OpenRead($FilePath)
    $fileContent = [System.Net.Http.StreamContent]::new($stream)
    $fileContent.Headers.ContentType =
        [System.Net.Http.Headers.MediaTypeHeaderValue]::new(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")
    $content.Add($fileContent, "file", [System.IO.Path]::GetFileName($FilePath))

    try {
        $response = $client.PostAsync("$api$Path", $content).GetAwaiter().GetResult()
        $json = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        if (-not $response.IsSuccessStatusCode) {
            throw "POST $Path failed ($([int]$response.StatusCode)): $json"
        }
        $json | ConvertFrom-Json
    }
    finally {
        $stream.Dispose()
        $content.Dispose()
        $client.Dispose()
    }
}

Write-Host "Connecting to $api"
$sessions = @{}
foreach ($person in $people) {
    $sessions[$person.Email] = Get-Session -Person $person
    Write-Host "Account ready: $($person.Email)"
}

$leaderToken = $sessions[$people[0].Email].accessToken
$workspaces = Invoke-JsonRequest -Method Get -Path "/workspaces/" -Token $leaderToken
$workspace = $workspaces | Where-Object { $_.name -eq "UIT Software Engineering Team" } | Select-Object -First 1
if (-not $workspace) {
    $workspace = Invoke-JsonRequest -Method Post -Path "/workspaces/" -Token $leaderToken -Body @{
        name = "UIT Software Engineering Team"
    }
    Write-Host "Workspace created."
}

$members = Invoke-JsonRequest -Method Get -Path "/workspaces/$($workspace.id)/members" -Token $leaderToken
foreach ($person in $people | Select-Object -Skip 1) {
    if (-not ($members | Where-Object { $_.email -eq $person.Email })) {
        Invoke-JsonRequest -Method Post -Path "/workspaces/$($workspace.id)/members" -Token $leaderToken -Body @{
            email = $person.Email
            role = $person.Role
        } | Out-Null
        Write-Host "Member added: $($person.Email)"
    }
}
$members = Invoke-JsonRequest -Method Get -Path "/workspaces/$($workspace.id)/members" -Token $leaderToken

$projects = Invoke-JsonRequest -Method Get -Path "/workspaces/$($workspace.id)/projects/" -Token $leaderToken
$project = $projects | Where-Object { $_.name -eq "CampusEvent" } | Select-Object -First 1
if (-not $project) {
    $project = Invoke-JsonRequest -Method Post -Path "/workspaces/$($workspace.id)/projects/" -Token $leaderToken -Body @{
        name = "CampusEvent"
        description = "Do an mon Cong nghe phan mem cua nhom 4 sinh vien UIT."
    }
    Write-Host "Project created."
}

$taskPage = Invoke-JsonRequest -Method Get -Path "/workspaces/$($workspace.id)/projects/$($project.id)/tasks/?pageSize=100" -Token $leaderToken
$tasks = @($taskPage.items)
$demoTasks = @($tasks | Where-Object { $_.code -match '^CE-\d{3}$' })
if ($demoTasks.Count -eq 0) {
    $upload = Send-Workbook -Path "/workspaces/$($workspace.id)/projects/$($project.id)/tasks/imports" -Token $leaderToken -FilePath $workbook
    $preview = Invoke-JsonRequest -Method Put -Path "/workspaces/$($workspace.id)/projects/$($project.id)/tasks/imports/$($upload.batchId)/preview" -Token $leaderToken -Body @{
        sheetName = "Tasks"
        mapping = @{
            taskCode = 1; title = 2; assigneeEmail = 3; status = 4
            priority = 5; dueDate = 6; estimateHours = 7; dependsOn = 8
        }
    }
    if ($preview.errorRows -ne 0 -or $preview.validRows -ne 20) {
        $errors = $preview.rows | Where-Object { $_.errors.Count -gt 0 } | ConvertTo-Json -Depth 8
        throw "Workbook validation failed. $errors"
    }
    $confirmation = Invoke-JsonRequest -Method Post -Path "/workspaces/$($workspace.id)/projects/$($project.id)/tasks/imports/$($upload.batchId)/confirm" -Token $leaderToken
    Write-Host "$($confirmation.importedTaskCount) tasks imported."
}
elseif ($demoTasks.Count -ne 20) {
    throw "Found $($demoTasks.Count) of 20 CE tasks. Refusing to create duplicates; use a clean database or complete the existing scenario."
}

$taskPage = Invoke-JsonRequest -Method Get -Path "/workspaces/$($workspace.id)/projects/$($project.id)/tasks/?pageSize=100" -Token $leaderToken
$tasksByCode = @{}
foreach ($task in $taskPage.items) { $tasksByCode[$task.code] = $task }

$evidence = @(
    @{ Code = "CE-106"; Email = "gia.huy@teampulse.demo"; Type = "PullRequest"; Url = "https://github.com/Btrung-uit/TeamPulse/pull/58"; Note = "Authentication API ready for leader review." },
    @{ Code = "CE-107"; Email = "minh.anh@teampulse.demo"; Type = "Design"; Url = "https://www.figma.com/community/file/1322037651638223280"; Note = "Login and registration UI design reference." },
    @{ Code = "CE-112"; Email = "gia.huy@teampulse.demo"; Type = "Document"; Url = "https://github.com/Btrung-uit/TeamPulse/blob/main/docs/api/tasks.md"; Note = "Event registration API contract and validation notes." }
)
foreach ($item in $evidence) {
    $task = $tasksByCode[$item.Code]
    $path = "/workspaces/$($workspace.id)/projects/$($project.id)/tasks/$($task.id)"
    $collaboration = Invoke-JsonRequest -Method Get -Path "$path/collaboration" -Token $leaderToken
    if (-not ($collaboration.evidence | Where-Object { $_.url -eq $item.Url })) {
        Invoke-JsonRequest -Method Post -Path "$path/evidence" -Token $sessions[$item.Email].accessToken -Body @{
            type = $item.Type; url = $item.Url; note = $item.Note
        } | Out-Null
        Write-Host "Evidence added: $($item.Code)"
    }
}

$reviewTask = Invoke-JsonRequest -Method Get -Path "/workspaces/$($workspace.id)/projects/$($project.id)/tasks/$($tasksByCode['CE-106'].id)" -Token $leaderToken
if ($reviewTask.status -eq "InReview") {
    Invoke-JsonRequest -Method Post -Path "/workspaces/$($workspace.id)/projects/$($project.id)/tasks/$($reviewTask.id)/review" -Token $leaderToken -Body @{
        decision = "Accept"
        comment = "Da kiem tra API, test va minh chung. Chap nhan cho demo mon hoc."
        rowVersion = $reviewTask.rowVersion
    } | Out-Null
    Write-Host "Review accepted: CE-106"
}

$resources = Invoke-JsonRequest -Method Get -Path "/workspaces/$($workspace.id)/projects/$($project.id)/resources/" -Token $leaderToken
$leaderUserId = $sessions[$people[0].Email].user.id
$resourceSeed = @(
    @{ Title = "TeamPulse source repository"; Type = "GitHub"; Url = "https://github.com/Btrung-uit/TeamPulse"; ExternalId = "Btrung-uit/TeamPulse"; TaskCode = "CE-103" },
    @{ Title = "CampusEvent API contract"; Type = "Other"; Url = "https://github.com/Btrung-uit/TeamPulse/blob/main/docs/api/tasks.md"; ExternalId = "docs/api/tasks.md"; TaskCode = "CE-118" },
    @{ Title = "CampusEvent presentation demo"; Type = "Demo"; Url = "http://localhost:8080"; ExternalId = "local-demo"; TaskCode = "CE-119" }
)
foreach ($item in $resourceSeed) {
    if (-not ($resources | Where-Object { $_.title -eq $item.Title })) {
        Invoke-JsonRequest -Method Post -Path "/workspaces/$($workspace.id)/projects/$($project.id)/resources/" -Token $leaderToken -Body @{
            title = $item.Title
            type = $item.Type
            url = $item.Url
            externalId = $item.ExternalId
            ownerUserId = $leaderUserId
            linkedTaskId = $tasksByCode[$item.TaskCode].id
        } | Out-Null
        Write-Host "Resource added: $($item.Title)"
    }
}

Write-Host ""
Write-Host "Demo scenario ready:"
Write-Host "  Workspace: $($workspace.name) ($($workspace.id))"
Write-Host "  Project:   $($project.name) ($($project.id))"
Write-Host "  Accounts:  $($people.Email -join ', ')"
Write-Host "  Password:  $DemoPassword"
