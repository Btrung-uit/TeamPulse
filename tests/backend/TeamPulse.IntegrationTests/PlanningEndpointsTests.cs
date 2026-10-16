using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TeamPulse.Domain.Activities;
using TeamPulse.Domain.Tasks;
using TeamPulse.Infrastructure.Persistence;

namespace TeamPulse.IntegrationTests;

public sealed class PlanningEndpointsTests : IClassFixture<TeamPulseApiFactory>
{
    private readonly TeamPulseApiFactory _factory;

    public PlanningEndpointsTests(TeamPulseApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SprintFlow_SupportsCrudAssignmentAndSafeDelete()
    {
        using var leader = await RegisterAsync("Sprint Flow Leader");
        var workspaceId = await CreateWorkspaceAsync(leader.Client, "Sprint Workspace");
        var project = await CreateProjectAsync(leader.Client, workspaceId, "Sprint Project");
        var task = await SeedTaskAsync(
            workspaceId,
            project.Id,
            leader.UserId,
            "TP-301",
            "Plan sprint work");

        var create = await leader.Client.PostAsJsonAsync(
            SprintsUrl(workspaceId, project.Id),
            new SaveSprintRequest(
                "Sprint 1",
                "Build planning",
                new DateOnly(2026, 10, 17),
                new DateOnly(2026, 10, 31)));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var sprint = await create.Content.ReadFromJsonAsync<SprintResponse>();
        Assert.NotNull(sprint);
        Assert.Equal(0, sprint.TaskCount);

        var duplicateName = await leader.Client.PostAsJsonAsync(
            SprintsUrl(workspaceId, project.Id),
            new SaveSprintRequest(
                "SPRINT 1",
                null,
                new DateOnly(2026, 11, 1),
                new DateOnly(2026, 11, 14)));
        await AssertErrorAsync(duplicateName, HttpStatusCode.Conflict, "sprint_name_conflict");

        var assign = await leader.Client.PutAsJsonAsync(
            $"{ProjectUrl(workspaceId, project.Id)}/tasks/{task.Id}/sprint",
            new AssignSprintRequest(sprint.Id));
        Assert.Equal(HttpStatusCode.OK, assign.StatusCode);
        var assignment = await assign.Content.ReadFromJsonAsync<TaskSprintAssignmentResponse>();
        Assert.NotNull(assignment);
        Assert.Equal(sprint.Id, assignment.SprintId);

        var listed = await leader.Client.GetFromJsonAsync<List<SprintResponse>>(
            SprintsUrl(workspaceId, project.Id));
        Assert.NotNull(listed);
        Assert.Equal(1, Assert.Single(listed).TaskCount);

        var deleteAssigned = await leader.Client.DeleteAsync(
            $"{SprintsUrl(workspaceId, project.Id)}/{sprint.Id}");
        await AssertErrorAsync(deleteAssigned, HttpStatusCode.Conflict, "sprint_has_tasks");

        var update = await leader.Client.PutAsJsonAsync(
            $"{SprintsUrl(workspaceId, project.Id)}/{sprint.Id}",
            new SaveSprintRequest(
                "Sprint Foundation",
                "Updated goal",
                new DateOnly(2026, 10, 18),
                new DateOnly(2026, 11, 1)));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var unassign = await leader.Client.PutAsJsonAsync(
            $"{ProjectUrl(workspaceId, project.Id)}/tasks/{task.Id}/sprint",
            new AssignSprintRequest(null));
        Assert.Equal(HttpStatusCode.OK, unassign.StatusCode);

        var delete = await leader.Client.DeleteAsync(
            $"{SprintsUrl(workspaceId, project.Id)}/{sprint.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TeamPulseDbContext>();
        var actions = await dbContext.ActivityLogs
            .IgnoreQueryFilters()
            .Where(activity =>
                activity.WorkspaceId == workspaceId &&
                (activity.EntityId == sprint.Id || activity.EntityId == task.Id))
            .Select(activity => activity.Action)
            .ToListAsync();

        Assert.Contains(ActivityAction.SprintCreated, actions);
        Assert.Contains(ActivityAction.SprintUpdated, actions);
        Assert.Contains(ActivityAction.SprintDeleted, actions);
        Assert.Equal(2, actions.Count(action => action == ActivityAction.TaskSprintChanged));
    }

    [Fact]
    public async Task DependencyFlow_RejectsCrossProjectAndCycles_AndReturnsRiskWarnings()
    {
        using var leader = await RegisterAsync("Dependency Leader");
        var workspaceId = await CreateWorkspaceAsync(leader.Client, "Dependency Workspace");
        var project = await CreateProjectAsync(leader.Client, workspaceId, "Dependency Project");
        var otherProject = await CreateProjectAsync(
            leader.Client,
            workspaceId,
            "Other Project");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var predecessor = await SeedTaskAsync(
            workspaceId,
            project.Id,
            leader.UserId,
            "TP-401",
            "Predecessor",
            today.AddDays(-1));
        var successor = await SeedTaskAsync(
            workspaceId,
            project.Id,
            leader.UserId,
            "TP-402",
            "Successor",
            today.AddDays(-2));
        var third = await SeedTaskAsync(
            workspaceId,
            project.Id,
            leader.UserId,
            "TP-403",
            "Third task",
            today.AddDays(5));
        var foreignTask = await SeedTaskAsync(
            workspaceId,
            otherProject.Id,
            leader.UserId,
            "TP-404",
            "Other project task");

        var create = await CreateDependencyAsync(
            leader.Client,
            workspaceId,
            project.Id,
            predecessor.Id,
            successor.Id);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var dependency = await create.Content.ReadFromJsonAsync<DependencyResponse>();
        Assert.NotNull(dependency);
        Assert.True(dependency.IsAtRisk);
        Assert.Equal(
            ["predecessor_incomplete", "predecessor_overdue", "deadline_conflict"],
            dependency.Risks.Select(risk => risk.Code).ToArray());

        var duplicate = await CreateDependencyAsync(
            leader.Client,
            workspaceId,
            project.Id,
            predecessor.Id,
            successor.Id);
        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);

        var secondEdge = await CreateDependencyAsync(
            leader.Client,
            workspaceId,
            project.Id,
            successor.Id,
            third.Id);
        Assert.Equal(HttpStatusCode.OK, secondEdge.StatusCode);

        var cycle = await CreateDependencyAsync(
            leader.Client,
            workspaceId,
            project.Id,
            third.Id,
            predecessor.Id);
        await AssertErrorAsync(cycle, HttpStatusCode.Conflict, "dependency_cycle");

        var self = await CreateDependencyAsync(
            leader.Client,
            workspaceId,
            project.Id,
            predecessor.Id,
            predecessor.Id);
        await AssertErrorAsync(self, HttpStatusCode.Conflict, "self_dependency");

        var crossProject = await CreateDependencyAsync(
            leader.Client,
            workspaceId,
            project.Id,
            predecessor.Id,
            foreignTask.Id);
        await AssertErrorAsync(crossProject, HttpStatusCode.NotFound, "task_not_found");

        var dependencies = await leader.Client.GetFromJsonAsync<List<DependencyResponse>>(
            $"{ProjectUrl(workspaceId, project.Id)}/dependencies");
        Assert.NotNull(dependencies);
        Assert.Equal(2, dependencies.Count);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<TeamPulseDbContext>();
            var persistedPredecessor = await dbContext.Tasks
                .IgnoreQueryFilters()
                .SingleAsync(task => task.Id == predecessor.Id);
            var persistedSuccessor = await dbContext.Tasks
                .IgnoreQueryFilters()
                .SingleAsync(task => task.Id == successor.Id);
            Assert.Equal(today.AddDays(-1), persistedPredecessor.DueOn);
            Assert.Equal(today.AddDays(-2), persistedSuccessor.DueOn);
            Assert.Equal(
                2,
                await dbContext.TaskDependencies
                    .IgnoreQueryFilters()
                    .CountAsync(item => item.ProjectId == project.Id));
        }

        var deleteUrl =
            $"{ProjectUrl(workspaceId, project.Id)}/dependencies/{predecessor.Id}/{successor.Id}";
        Assert.Equal(HttpStatusCode.NoContent, (await leader.Client.DeleteAsync(deleteUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await leader.Client.DeleteAsync(deleteUrl)).StatusCode);
    }

    [Theory]
    [InlineData("Member")]
    [InlineData("Advisor")]
    public async Task ReadOnlyRoles_CanReadPlanningButCannotMutateIt(string role)
    {
        using var leader = await RegisterAsync($"{role} Planning Leader");
        using var reader = await RegisterAsync($"{role} Planning Reader");
        var workspaceId = await CreateWorkspaceAsync(leader.Client, $"{role} Workspace");
        await AddMemberAsync(leader.Client, workspaceId, reader.Email, role);
        var project = await CreateProjectAsync(leader.Client, workspaceId, $"{role} Project");
        var task = await SeedTaskAsync(
            workspaceId,
            project.Id,
            leader.UserId,
            $"{role.ToUpperInvariant()}-1",
            "Protected planning task");

        var listSprints = await reader.Client.GetAsync(SprintsUrl(workspaceId, project.Id));
        var listDependencies = await reader.Client.GetAsync(
            $"{ProjectUrl(workspaceId, project.Id)}/dependencies");
        Assert.Equal(HttpStatusCode.OK, listSprints.StatusCode);
        Assert.Equal(HttpStatusCode.OK, listDependencies.StatusCode);

        var createSprint = await reader.Client.PostAsJsonAsync(
            SprintsUrl(workspaceId, project.Id),
            new SaveSprintRequest(
                "Forbidden sprint",
                null,
                new DateOnly(2026, 10, 17),
                new DateOnly(2026, 10, 31)));
        var assignSprint = await reader.Client.PutAsJsonAsync(
            $"{ProjectUrl(workspaceId, project.Id)}/tasks/{task.Id}/sprint",
            new AssignSprintRequest(null));
        var createDependency = await CreateDependencyAsync(
            reader.Client,
            workspaceId,
            project.Id,
            task.Id,
            Guid.NewGuid());

        Assert.All(
            new[] { createSprint, assignSprint, createDependency },
            response => Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode));
    }

    [Fact]
    public async Task PlanningRoutes_HideForeignProjectsAndRejectArchivedProjectMutations()
    {
        using var leaderA = await RegisterAsync("Planning Tenant A");
        using var leaderB = await RegisterAsync("Planning Tenant B");
        var workspaceA = await CreateWorkspaceAsync(leaderA.Client, "Planning Workspace A");
        var workspaceB = await CreateWorkspaceAsync(leaderB.Client, "Planning Workspace B");
        var projectA = await CreateProjectAsync(leaderA.Client, workspaceA, "Planning Project A");
        var projectB = await CreateProjectAsync(leaderB.Client, workspaceB, "Planning Project B");

        var forbiddenTenant = await leaderA.Client.GetAsync(SprintsUrl(workspaceB, projectB.Id));
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenTenant.StatusCode);

        var disguisedProject = await leaderA.Client.GetAsync(SprintsUrl(workspaceA, projectB.Id));
        await AssertErrorAsync(disguisedProject, HttpStatusCode.NotFound, "project_not_found");

        var archive = await leaderA.Client.PostAsync(
            $"/api/workspaces/{workspaceA}/projects/{projectA.Id}/archive",
            content: null);
        Assert.Equal(HttpStatusCode.OK, archive.StatusCode);

        var createOnArchived = await leaderA.Client.PostAsJsonAsync(
            SprintsUrl(workspaceA, projectA.Id),
            new SaveSprintRequest(
                "Too late",
                null,
                new DateOnly(2026, 10, 17),
                new DateOnly(2026, 10, 31)));
        await AssertErrorAsync(createOnArchived, HttpStatusCode.Conflict, "project_archived");
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
        var workspace = await response.Content.ReadFromJsonAsync<WorkspaceResponse>();
        Assert.NotNull(workspace);
        return workspace.Id;
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

    private static async Task<ProjectResponse> CreateProjectAsync(
        HttpClient client,
        Guid workspaceId,
        string name)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/workspaces/{workspaceId}/projects",
            new { name });
        response.EnsureSuccessStatusCode();
        var project = await response.Content.ReadFromJsonAsync<ProjectResponse>();
        Assert.NotNull(project);
        return project;
    }

    private async Task<ProjectTask> SeedTaskAsync(
        Guid workspaceId,
        Guid projectId,
        Guid creatorUserId,
        string code,
        string title,
        DateOnly? dueOn = null)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TeamPulseDbContext>();
        var task = new ProjectTask(
            Guid.NewGuid(),
            workspaceId,
            projectId,
            creatorUserId,
            TaskCode.Parse(code),
            title);
        if (dueOn.HasValue)
        {
            task.Schedule(dueOn, 4);
        }

        dbContext.Tasks.Add(task);
        await dbContext.SaveChangesAsync();
        return task;
    }

    private static Task<HttpResponseMessage> CreateDependencyAsync(
        HttpClient client,
        Guid workspaceId,
        Guid projectId,
        Guid predecessorTaskId,
        Guid successorTaskId) =>
        client.PostAsJsonAsync(
            $"{ProjectUrl(workspaceId, projectId)}/dependencies",
            new { predecessorTaskId, successorTaskId });

    private static async Task AssertErrorAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedCode)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal(expectedCode, error.Code);
    }

    private static string ProjectUrl(Guid workspaceId, Guid projectId) =>
        $"/api/workspaces/{workspaceId}/projects/{projectId}";

    private static string SprintsUrl(Guid workspaceId, Guid projectId) =>
        $"{ProjectUrl(workspaceId, projectId)}/sprints";

    private sealed record RegisteredUser(Guid UserId, string Email, HttpClient Client) : IDisposable
    {
        public void Dispose() => Client.Dispose();
    }

    private sealed record AuthSessionResponse(AuthUserResponse User, string AccessToken);

    private sealed record AuthUserResponse(Guid Id);

    private sealed record WorkspaceResponse(Guid Id);

    private sealed record ProjectResponse(Guid Id);

    private sealed record SaveSprintRequest(
        string Name,
        string? Goal,
        DateOnly StartsOn,
        DateOnly EndsOn);

    private sealed record AssignSprintRequest(Guid? SprintId);

    private sealed record SprintResponse(Guid Id, int TaskCount);

    private sealed record TaskSprintAssignmentResponse(
        Guid TaskId,
        string TaskCode,
        Guid? SprintId,
        string? SprintName);

    private sealed record DependencyRiskResponse(string Code, string Message);

    private sealed record DependencyResponse(
        bool IsAtRisk,
        IReadOnlyList<DependencyRiskResponse> Risks);

    private sealed record ApiErrorResponse(string Code, string Message);
}
