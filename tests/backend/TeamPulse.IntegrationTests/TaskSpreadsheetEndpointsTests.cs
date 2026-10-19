using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc.Testing;

namespace TeamPulse.IntegrationTests;

public sealed class TaskSpreadsheetEndpointsTests : IClassFixture<TeamPulseApiFactory>
{
    private readonly TeamPulseApiFactory _factory;

    public TaskSpreadsheetEndpointsTests(TeamPulseApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task DemoSeedWorkbook_IsReadableByTheImportEndpoint()
    {
        using var leader = await RegisterAsync("Demo Seed Leader");
        var workspaceId = await CreateWorkspaceAsync(leader.Client, "Demo Seed Workspace");
        var projectId = await CreateProjectAsync(leader.Client, workspaceId, "Demo Seed Project");
        var seedPath = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "TeamPulse-Demo-Tasks.xlsx");
        var bytes = await File.ReadAllBytesAsync(seedPath);
        using (var workbook = new XLWorkbook(new MemoryStream(bytes)))
        {
            var dueDate = workbook.Worksheet("Tasks").Cell("F2");
            Assert.Equal(XLDataType.DateTime, dueDate.DataType);
            Assert.Equal(new DateTime(2026, 10, 6), dueDate.GetDateTime());
            Assert.Equal("yyyy-mm-dd", dueDate.Style.DateFormat.Format);
        }

        using var content = MultipartWorkbook(bytes, "TeamPulse-Demo-Tasks.xlsx");
        var response = await leader.Client.PostAsync(
            $"{TasksUrl(workspaceId, projectId)}/imports",
            content);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var sheet = json.RootElement.GetProperty("sheets")[0];
        Assert.Equal("Tasks", sheet.GetProperty("name").GetString());
        Assert.Equal(20, sheet.GetProperty("dataRowCount").GetInt32());
    }

    [Fact]
    public async Task Template_IsLeaderOnlyAndContainsImportReadyStructure()
    {
        using var leader = await RegisterAsync("Template Leader");
        using var member = await RegisterAsync("Template Member");
        var workspaceId = await CreateWorkspaceAsync(leader.Client, "Template Workspace");
        await AddMemberAsync(leader.Client, workspaceId, member.Email, "Member");
        var projectId = await CreateProjectAsync(leader.Client, workspaceId, "Excel Onboarding");
        var url = $"{TasksUrl(workspaceId, projectId)}/template";

        Assert.Equal(HttpStatusCode.Forbidden, (await member.Client.GetAsync(url)).StatusCode);

        var response = await leader.Client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(
            "excel-onboarding-task-template.xlsx",
            response.Content.Headers.ContentDisposition?.FileNameStar);

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var workbook = new XLWorkbook(stream);
        var tasks = workbook.Worksheet("Tasks");
        var example = workbook.Worksheet("Example - do not import");

        Assert.Equal(ExpectedHeaders, tasks.Row(1).Cells(1, 8).Select(cell => cell.GetString()));
        Assert.True(tasks.Row(2).Cells(1, 8).All(cell => cell.IsEmpty()));
        Assert.Equal("TP-001", example.Cell("A2").GetString());
        Assert.Equal("Todo", example.Cell("D2").GetString());
        Assert.Equal("yyyy-mm-dd", example.Cell("F2").Style.DateFormat.Format);
        Assert.Equal(XLDataType.DateTime, example.Cell("F2").DataType);
        Assert.NotEmpty(tasks.DataValidations);
    }

    [Fact]
    public async Task Export_MatchesProjectFiltersAndIncludesTypedDependencySnapshot()
    {
        using var leader = await RegisterAsync("Export Leader");
        using var member = await RegisterAsync("Export Member");
        using var outsider = await RegisterAsync("Export Outsider");
        var workspaceId = await CreateWorkspaceAsync(leader.Client, "Export Workspace");
        await AddMemberAsync(leader.Client, workspaceId, member.Email, "Member");
        var projectId = await CreateProjectAsync(leader.Client, workspaceId, "TeamPulse MVP");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var first = await CreateTaskAsync(
            leader.Client,
            workspaceId,
            projectId,
            "TP-040",
            "=HYPERLINK(\"https://example.com\",\"click\")",
            member.UserId,
            "High",
            today,
            8m);
        await CreateTaskAsync(
            leader.Client,
            workspaceId,
            projectId,
            "TP-041",
            "Future import wizard",
            member.UserId,
            "Low",
            today.AddDays(30),
            13m);
        var dependent = await CreateTaskAsync(
            leader.Client,
            workspaceId,
            projectId,
            "TP-042",
            "Confirm import",
            member.UserId,
            "High",
            today.AddDays(2),
            5.5m);
        await CreateDependencyAsync(
            leader.Client,
            workspaceId,
            projectId,
            first.Id,
            dependent.Id);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await member.Client.GetAsync(
                $"{TasksUrl(workspaceId, projectId)}/export")).StatusCode);

        var filtered = await leader.Client.GetAsync(
            $"{TasksUrl(workspaceId, projectId)}/export" +
            $"?priority=High&assigneeUserId={member.UserId}&sort=code-asc");
        filtered.EnsureSuccessStatusCode();
        using var workbook = await ReadWorkbookAsync(filtered);
        var sheet = workbook.Worksheet("Tasks");

        Assert.Equal(ExpectedHeaders, sheet.Row(1).Cells(1, 8).Select(cell => cell.GetString()));
        Assert.Equal("TP-040", sheet.Cell("A2").GetString());
        Assert.Equal("TP-042", sheet.Cell("A3").GetString());
        Assert.True(sheet.Cell("A4").IsEmpty());
        Assert.Equal(member.Email, sheet.Cell("C2").GetString());
        Assert.Equal(XLDataType.Text, sheet.Cell("B2").DataType);
        Assert.False(sheet.Cell("B2").HasFormula);
        Assert.Equal("TP-040", sheet.Cell("H3").GetString());
        Assert.Equal(XLDataType.DateTime, sheet.Cell("F2").DataType);
        Assert.Equal(today.ToDateTime(TimeOnly.MinValue), sheet.Cell("F2").GetDateTime());
        Assert.Equal(XLDataType.Number, sheet.Cell("G3").DataType);
        Assert.Equal(5.5, sheet.Cell("G3").GetDouble());

        var attention = await leader.Client.GetAsync(
            $"{TasksUrl(workspaceId, projectId)}/export?attention=true&sort=code-asc");
        attention.EnsureSuccessStatusCode();
        using var attentionWorkbook = await ReadWorkbookAsync(attention);
        var attentionCodes = attentionWorkbook.Worksheet("Tasks")
            .Column(1)
            .CellsUsed()
            .Skip(1)
            .Select(cell => cell.GetString())
            .ToArray();
        Assert.Equal(["TP-040", "TP-042"], attentionCodes);

        var invalidSort = await leader.Client.GetAsync(
            $"{TasksUrl(workspaceId, projectId)}/export?sort=unknown");
        Assert.Equal(HttpStatusCode.BadRequest, invalidSort.StatusCode);

        var outsiderWorkspaceId = await CreateWorkspaceAsync(
            outsider.Client,
            "Other Export Workspace");
        var outsiderProjectId = await CreateProjectAsync(
            outsider.Client,
            outsiderWorkspaceId,
            "Other Export Project");
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await leader.Client.GetAsync(
                $"{TasksUrl(outsiderWorkspaceId, outsiderProjectId)}/export")).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await leader.Client.GetAsync(
                $"{TasksUrl(workspaceId, outsiderProjectId)}/export")).StatusCode);
    }

    [Fact]
    public async Task Import_IsLeaderOnlyAndPreviewReportsCellErrorsWithoutCreatingTasks()
    {
        using var leader = await RegisterAsync("Import Leader");
        using var member = await RegisterAsync("Import Member");
        var workspaceId = await CreateWorkspaceAsync(leader.Client, "Import Workspace");
        await AddMemberAsync(leader.Client, workspaceId, member.Email, "Member");
        var projectId = await CreateProjectAsync(leader.Client, workspaceId, "Import Project");
        await CreateTaskAsync(
            leader.Client,
            workspaceId,
            projectId,
            "TP-100",
            "Existing task",
            member.UserId,
            "Medium",
            new DateOnly(2026, 11, 1),
            2m);
        var bytes = CreateImportWorkbook(member.Email);
        var importUrl = $"{TasksUrl(workspaceId, projectId)}/imports";

        using (var forbiddenContent = MultipartWorkbook(bytes, "tasks.xlsx"))
        {
            Assert.Equal(
                HttpStatusCode.Forbidden,
                (await member.Client.PostAsync(importUrl, forbiddenContent)).StatusCode);
        }

        using var content = MultipartWorkbook(bytes, "tasks.xlsx");
        var upload = await leader.Client.PostAsync(importUrl, content);
        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        using var uploaded = JsonDocument.Parse(await upload.Content.ReadAsStringAsync());
        var root = uploaded.RootElement;
        var batchId = root.GetProperty("batchId").GetGuid();
        Assert.Equal("Plan", root.GetProperty("sheets")[0].GetProperty("name").GetString());
        Assert.Equal(3, root.GetProperty("sheets")[0].GetProperty("dataRowCount").GetInt32());

        var preview = await leader.Client.PutAsJsonAsync(
            $"{importUrl}/{batchId}/preview",
            new
            {
                sheetName = "Plan",
                mapping = new
                {
                    taskCode = 1,
                    title = 2,
                    assigneeEmail = 3,
                    status = 4,
                    priority = 5,
                    dueDate = 6,
                    estimateHours = 7,
                    dependsOn = 8,
                },
            });
        preview.EnsureSuccessStatusCode();
        using var previewJson = JsonDocument.Parse(await preview.Content.ReadAsStringAsync());
        var previewRoot = previewJson.RootElement;
        Assert.Equal("ValidationFailed", previewRoot.GetProperty("status").GetString());
        Assert.Equal(3, previewRoot.GetProperty("totalRows").GetInt32());
        Assert.Equal(0, previewRoot.GetProperty("validRows").GetInt32());
        Assert.Equal(3, previewRoot.GetProperty("errorRows").GetInt32());
        var reasons = previewRoot.GetProperty("rows")
            .EnumerateArray()
            .SelectMany(row => row.GetProperty("errors").EnumerateArray())
            .Select(error => error.GetProperty("reason").GetString())
            .ToArray();
        Assert.Contains(reasons, reason => reason!.Contains("already exists", StringComparison.Ordinal));
        Assert.Contains(reasons, reason => reason!.Contains("duplicated", StringComparison.Ordinal));
        Assert.Contains(reasons, reason => reason!.Contains("active workspace member", StringComparison.Ordinal));
        Assert.Contains(reasons, reason => reason!.Contains("valid Excel date", StringComparison.Ordinal));
        Assert.Contains(reasons, reason => reason!.Contains("cannot depend on itself", StringComparison.Ordinal));

        var rejectedConfirm = await leader.Client.PostAsync(
            $"{importUrl}/{batchId}/confirm",
            null);
        Assert.Equal(HttpStatusCode.Conflict, rejectedConfirm.StatusCode);

        var tasks = await leader.Client.GetFromJsonAsync<JsonElement>(TasksUrl(workspaceId, projectId));
        Assert.Equal(1, tasks.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Confirm_CreatesWholeBatchOnceAndRecordsHistoryAndActivity()
    {
        using var leader = await RegisterAsync("Confirm Leader");
        using var member = await RegisterAsync("Confirm Member");
        var workspaceId = await CreateWorkspaceAsync(leader.Client, "Confirm Workspace");
        await AddMemberAsync(leader.Client, workspaceId, member.Email, "Member");
        var projectId = await CreateProjectAsync(leader.Client, workspaceId, "Confirm Project");
        var importUrl = $"{TasksUrl(workspaceId, projectId)}/imports";
        using var content = MultipartWorkbook(CreateValidImportWorkbook(member.Email), "valid.xlsx");
        var upload = await leader.Client.PostAsync(importUrl, content);
        upload.EnsureSuccessStatusCode();
        using var uploadJson = JsonDocument.Parse(await upload.Content.ReadAsStringAsync());
        var batchId = uploadJson.RootElement.GetProperty("batchId").GetGuid();

        var preview = await leader.Client.PutAsJsonAsync(
            $"{importUrl}/{batchId}/preview",
            new
            {
                sheetName = "Tasks",
                mapping = new
                {
                    taskCode = 1,
                    title = 2,
                    assigneeEmail = 3,
                    status = 4,
                    priority = 5,
                    dueDate = 6,
                    estimateHours = 7,
                    dependsOn = 8,
                },
            });
        preview.EnsureSuccessStatusCode();
        Assert.Equal(
            0,
            (await preview.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("errorRows")
                .GetInt32());
        Assert.Equal(HttpStatusCode.Forbidden, (await member.Client.GetAsync(importUrl)).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await member.Client.PostAsync($"{importUrl}/{batchId}/confirm", null)).StatusCode);

        var firstConfirm = await leader.Client.PostAsync(
            $"{importUrl}/{batchId}/confirm",
            null);
        firstConfirm.EnsureSuccessStatusCode();
        var firstResult = await firstConfirm.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, firstResult.GetProperty("importedTaskCount").GetInt32());
        Assert.False(firstResult.GetProperty("alreadyConfirmed").GetBoolean());

        var retryConfirm = await leader.Client.PostAsync(
            $"{importUrl}/{batchId}/confirm",
            null);
        retryConfirm.EnsureSuccessStatusCode();
        var retryResult = await retryConfirm.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, retryResult.GetProperty("importedTaskCount").GetInt32());
        Assert.True(retryResult.GetProperty("alreadyConfirmed").GetBoolean());

        var tasks = await leader.Client.GetFromJsonAsync<JsonElement>(TasksUrl(workspaceId, projectId));
        Assert.Equal(2, tasks.GetProperty("totalCount").GetInt32());
        var dependencies = await leader.Client.GetFromJsonAsync<JsonElement>(
            $"/api/workspaces/{workspaceId}/projects/{projectId}/dependencies");
        Assert.Equal(1, dependencies.GetArrayLength());

        var history = await leader.Client.GetFromJsonAsync<JsonElement>(importUrl);
        Assert.Equal("Confirmed", history[0].GetProperty("status").GetString());
        Assert.Equal(2, history[0].GetProperty("importedTaskCount").GetInt32());

        var dashboard = await leader.Client.GetFromJsonAsync<JsonElement>(
            $"/api/workspaces/{workspaceId}/projects/{projectId}/dashboard");
        Assert.Equal(
            2,
            dashboard.GetProperty("recentActivity")
                .EnumerateArray()
                .Count(activity => activity.GetProperty("action").GetString() == "TaskCreated"));
    }

    [Fact]
    public async Task Import_RejectsInvalidFormatAndOversizedFilesOnServer()
    {
        using var leader = await RegisterAsync("Import Guard Leader");
        var workspaceId = await CreateWorkspaceAsync(leader.Client, "Import Guard Workspace");
        var projectId = await CreateProjectAsync(leader.Client, workspaceId, "Import Guard Project");
        using var content = MultipartWorkbook(CreateImportWorkbook(leader.Email), "tasks.csv");

        var response = await leader.Client.PostAsync(
            $"{TasksUrl(workspaceId, projectId)}/imports",
            content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var malformed = MultipartWorkbook(
            "this-is-not-an-excel-workbook"u8.ToArray(),
            "tasks.xlsx");
        var malformedResponse = await leader.Client.PostAsync(
            $"{TasksUrl(workspaceId, projectId)}/imports",
            malformed);
        Assert.Equal(HttpStatusCode.BadRequest, malformedResponse.StatusCode);

        using var oversized = MultipartWorkbook(
            new byte[(5 * 1024 * 1024) + 1],
            "tasks.xlsx");
        var oversizedResponse = await leader.Client.PostAsync(
            $"{TasksUrl(workspaceId, projectId)}/imports",
            oversized);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, oversizedResponse.StatusCode);
    }

    private async Task<RegisteredUser> RegisterAsync(string displayName)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });
        var email =
            $"{displayName.Replace(" ", string.Empty).ToLowerInvariant()}-{Guid.NewGuid():N}@example.com";
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            displayName,
            password = "StrongPass123",
        });
        response.EnsureSuccessStatusCode();
        var session = await response.Content.ReadFromJsonAsync<AuthSessionResponse>();
        Assert.NotNull(session);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", session.AccessToken);
        return new RegisteredUser(session.User.Id, email, client);
    }

    private static async Task<Guid> CreateWorkspaceAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/workspaces", new { name });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResponse>())!.Id;
    }

    private static async Task AddMemberAsync(
        HttpClient client,
        Guid workspaceId,
        string email,
        string role)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/workspaces/{workspaceId}/members",
            new { email, role });
        response.EnsureSuccessStatusCode();
    }

    private static async Task<Guid> CreateProjectAsync(
        HttpClient client,
        Guid workspaceId,
        string name)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/workspaces/{workspaceId}/projects",
            new { name });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResponse>())!.Id;
    }

    private static async Task<TaskResponse> CreateTaskAsync(
        HttpClient client,
        Guid workspaceId,
        Guid projectId,
        string code,
        string title,
        Guid assigneeUserId,
        string priority,
        DateOnly dueOn,
        decimal estimateHours)
    {
        var response = await client.PostAsJsonAsync(
            TasksUrl(workspaceId, projectId),
            new
            {
                code,
                title,
                assigneeUserId,
                priority,
                dueOn,
                estimateHours,
                rowVersion = (string?)null,
            });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TaskResponse>())!;
    }

    private static async Task CreateDependencyAsync(
        HttpClient client,
        Guid workspaceId,
        Guid projectId,
        Guid predecessorTaskId,
        Guid successorTaskId)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/workspaces/{workspaceId}/projects/{projectId}/dependencies",
            new { predecessorTaskId, successorTaskId });
        response.EnsureSuccessStatusCode();
    }

    private static async Task<XLWorkbook> ReadWorkbookAsync(HttpResponseMessage response)
    {
        var bytes = await response.Content.ReadAsByteArrayAsync();
        return new XLWorkbook(new MemoryStream(bytes));
    }

    private static byte[] CreateImportWorkbook(string memberEmail)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Plan");
        for (var index = 0; index < ExpectedHeaders.Length; index++)
        {
            sheet.Cell(1, index + 1).SetValue(ExpectedHeaders[index]);
        }

        sheet.Cell("A2").SetValue("TP-100");
        sheet.Cell("B2").SetValue("Conflicts with existing task");
        sheet.Cell("C2").SetValue(memberEmail);
        sheet.Cell("D2").SetValue("Todo");
        sheet.Cell("E2").SetValue("High");
        sheet.Cell("F2").SetValue(new DateTime(2026, 11, 2));
        sheet.Cell("G2").SetValue(3);

        sheet.Cell("A3").SetValue("TP-101");
        sheet.Cell("B3").SetValue("Invalid mapped values");
        sheet.Cell("C3").SetValue("unknown@example.com");
        sheet.Cell("D3").SetValue("Unknown");
        sheet.Cell("E3").SetValue("Extreme");
        sheet.Cell("F3").SetValue("not-a-date");
        sheet.Cell("G3").SetValue(-2);
        sheet.Cell("H3").SetValue("TP-101");

        sheet.Cell("A4").SetValue("TP-101");
        sheet.Cell("B4").SetValue("Duplicate code");
        sheet.Cell("D4").SetValue("Todo");
        sheet.Cell("E4").SetValue("Low");
        sheet.Cell("H4").SetValue("TP-999");

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static byte[] CreateValidImportWorkbook(string memberEmail)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Tasks");
        for (var index = 0; index < ExpectedHeaders.Length; index++)
        {
            sheet.Cell(1, index + 1).SetValue(ExpectedHeaders[index]);
        }

        sheet.Cell("A2").SetValue("TP-201");
        sheet.Cell("B2").SetValue("Imported predecessor");
        sheet.Cell("C2").SetValue(memberEmail);
        sheet.Cell("D2").SetValue("Done");
        sheet.Cell("E2").SetValue("High");
        sheet.Cell("F2").SetValue(new DateTime(2026, 12, 1));
        sheet.Cell("G2").SetValue(5.5);

        sheet.Cell("A3").SetValue("TP-202");
        sheet.Cell("B3").SetValue("Imported successor");
        sheet.Cell("C3").SetValue(memberEmail);
        sheet.Cell("D3").SetValue("Todo");
        sheet.Cell("E3").SetValue("Medium");
        sheet.Cell("F3").SetValue(new DateTime(2026, 12, 2));
        sheet.Cell("G3").SetValue(3);
        sheet.Cell("H3").SetValue("TP-201");

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static MultipartFormDataContent MultipartWorkbook(byte[] bytes, string fileName)
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        content.Add(file, "file", fileName);
        return content;
    }

    private static string TasksUrl(Guid workspaceId, Guid projectId) =>
        $"/api/workspaces/{workspaceId}/projects/{projectId}/tasks";

    private static readonly string[] ExpectedHeaders =
    [
        "TaskCode",
        "Title",
        "AssigneeEmail",
        "Status",
        "Priority",
        "DueDate",
        "EstimateHours",
        "DependsOn",
    ];

    private sealed record RegisteredUser(Guid UserId, string Email, HttpClient Client) : IDisposable
    {
        public void Dispose() => Client.Dispose();
    }

    private sealed record AuthSessionResponse(AuthUserResponse User, string AccessToken);

    private sealed record AuthUserResponse(Guid Id);

    private sealed record IdResponse(Guid Id);

    private sealed record TaskResponse(Guid Id);
}
