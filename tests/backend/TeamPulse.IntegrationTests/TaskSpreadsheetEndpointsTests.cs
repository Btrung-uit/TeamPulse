using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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
