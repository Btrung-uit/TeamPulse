using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TeamPulse.Domain.Activities;
using TeamPulse.Infrastructure.Persistence;

namespace TeamPulse.IntegrationTests;

public sealed class WorkspaceMembershipEndpointsTests : IClassFixture<TeamPulseApiFactory>
{
    private readonly TeamPulseApiFactory _factory;

    public WorkspaceMembershipEndpointsTests(TeamPulseApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task WorkspacePolicies_EnforceTenantAndRoleBoundaries_WithActivityHistory()
    {
        var leader = await RegisterAsync("Leader");
        var member = await RegisterAsync("Member");
        var advisor = await RegisterAsync("Advisor");
        var outsider = await RegisterAsync("Outsider");

        var workspaceA = await CreateWorkspaceAsync(leader.Client, "Workspace A");
        var workspaceB = await CreateWorkspaceAsync(outsider.Client, "Workspace B");

        var addMember = await leader.Client.PostAsJsonAsync(
            $"/api/workspaces/{workspaceA}/members",
            new { member.Email, role = "Member" });
        Assert.Equal(HttpStatusCode.Created, addMember.StatusCode);

        var addAdvisor = await leader.Client.PostAsJsonAsync(
            $"/api/workspaces/{workspaceA}/members",
            new { advisor.Email, role = "Advisor" });
        Assert.Equal(HttpStatusCode.Created, addAdvisor.StatusCode);

        var outsiderWorkspace = await leader.Client.GetAsync(
            $"/api/workspaces/{workspaceB}/members");
        Assert.Equal(HttpStatusCode.Forbidden, outsiderWorkspace.StatusCode);

        var advisorRead = await advisor.Client.GetAsync(
            $"/api/workspaces/{workspaceA}/members");
        Assert.Equal(HttpStatusCode.OK, advisorRead.StatusCode);

        var advisorMutation = await advisor.Client.PostAsJsonAsync(
            $"/api/workspaces/{workspaceA}/members",
            new { outsider.Email, role = "Member" });
        Assert.Equal(HttpStatusCode.Forbidden, advisorMutation.StatusCode);

        var memberEscalation = await member.Client.PatchAsJsonAsync(
            $"/api/workspaces/{workspaceA}/members/{member.UserId}/role",
            new { role = "Leader" });
        Assert.Equal(HttpStatusCode.Forbidden, memberEscalation.StatusCode);

        var roleChange = await leader.Client.PatchAsJsonAsync(
            $"/api/workspaces/{workspaceA}/members/{member.UserId}/role",
            new { role = "Advisor" });
        Assert.Equal(HttpStatusCode.OK, roleChange.StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TeamPulseDbContext>();
        var activities = await dbContext.ActivityLogs
            .IgnoreQueryFilters()
            .Where(activity => activity.WorkspaceId == workspaceA)
            .ToListAsync();

        Assert.Equal(5, activities.Count);
        Assert.Equal(
            3,
            activities.Count(activity =>
                activity.Action == ActivityAction.WorkspaceMemberAdded));
        Assert.Single(activities, activity =>
            activity.Action == ActivityAction.WorkspaceMemberRoleChanged &&
            activity.SubjectUserId == member.UserId &&
            activity.PreviousValue == "Member" &&
            activity.NewValue == "Advisor");
    }

    [Fact]
    public async Task FinalLeader_CannotDemoteThemself()
    {
        var leader = await RegisterAsync("Solo Leader");
        var workspaceId = await CreateWorkspaceAsync(leader.Client, "Solo Workspace");

        var response = await leader.Client.PatchAsJsonAsync(
            $"/api/workspaces/{workspaceId}/members/{leader.UserId}/role",
            new { role = "Member" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiError>();
        Assert.Equal("last_leader", error?.Code);
    }

    [Fact]
    public async Task AddMember_RejectsNumericRoleValues()
    {
        var leader = await RegisterAsync("Role Validator");
        var candidate = await RegisterAsync("Role Candidate");
        var workspaceId = await CreateWorkspaceAsync(leader.Client, "Validated Workspace");

        var response = await leader.Client.PostAsJsonAsync(
            $"/api/workspaces/{workspaceId}/members",
            new { candidate.Email, role = "0" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiError>();
        Assert.Equal("validation_failed", error?.Code);
    }

    [Fact]
    public async Task WorkspaceList_ReturnsOnlyActiveMembershipsForCurrentUser()
    {
        var leader = await RegisterAsync("Workspace Owner");
        var firstId = await CreateWorkspaceAsync(leader.Client, "Alpha Team");
        var secondId = await CreateWorkspaceAsync(leader.Client, "Beta Team");

        var workspaces = await leader.Client.GetFromJsonAsync<List<WorkspaceResponse>>(
            "/api/workspaces");

        Assert.NotNull(workspaces);
        Assert.Equal(2, workspaces.Count);
        Assert.Equal([firstId, secondId], workspaces.Select(workspace => workspace.Id).ToArray());
        Assert.All(workspaces, workspace => Assert.Equal("Leader", workspace.Role));
        Assert.All(workspaces, workspace => Assert.Equal(1, workspace.MemberCount));
    }

    [Fact]
    public async Task WorkspaceInvitation_AddsMatchingRegisteredUser_AndCannotBeReused()
    {
        var leader = await RegisterAsync("Invitation Leader");
        var workspaceId = await CreateWorkspaceAsync(leader.Client, "Invitation Workspace");
        var candidateEmail = $"invited-{Guid.NewGuid():N}@example.com";

        var inviteResponse = await leader.Client.PostAsJsonAsync(
            $"/api/workspaces/{workspaceId}/invitations",
            new { email = candidateEmail, role = "Member" });

        Assert.Equal(HttpStatusCode.Accepted, inviteResponse.StatusCode);
        var invitationEmail = _factory.Services
            .GetRequiredService<TestTransactionalEmailSender>()
            .Emails.Single(message =>
                message.Kind == "workspace-invitation" &&
                message.Recipient == candidateEmail);

        var candidate = await RegisterAsync("Invited Candidate", candidateEmail);
        var acceptResponse = await candidate.Client.PostAsJsonAsync(
            "/api/workspace-invitations/accept",
            new { token = invitationEmail.Token });

        Assert.Equal(HttpStatusCode.OK, acceptResponse.StatusCode);
        var workspaces = await candidate.Client.GetFromJsonAsync<List<WorkspaceResponse>>(
            "/api/workspaces");
        Assert.Contains(workspaces!, workspace =>
            workspace.Id == workspaceId && workspace.Role == "Member");

        var reusedInvitation = await candidate.Client.PostAsJsonAsync(
            "/api/workspace-invitations/accept",
            new { token = invitationEmail.Token });
        Assert.Equal(HttpStatusCode.BadRequest, reusedInvitation.StatusCode);
    }

    [Fact]
    public async Task WorkspaceInvitation_RejectsAccountWithDifferentEmail()
    {
        var leader = await RegisterAsync("Mismatch Leader");
        var otherUser = await RegisterAsync("Mismatch User");
        var workspaceId = await CreateWorkspaceAsync(leader.Client, "Private Workspace");
        var intendedEmail = $"intended-{Guid.NewGuid():N}@example.com";

        var inviteResponse = await leader.Client.PostAsJsonAsync(
            $"/api/workspaces/{workspaceId}/invitations",
            new { email = intendedEmail, role = "Advisor" });
        Assert.Equal(HttpStatusCode.Accepted, inviteResponse.StatusCode);
        var invitationEmail = _factory.Services
            .GetRequiredService<TestTransactionalEmailSender>()
            .Emails.Single(message =>
                message.Kind == "workspace-invitation" &&
                message.Recipient == intendedEmail);

        var response = await otherUser.Client.PostAsJsonAsync(
            "/api/workspace-invitations/accept",
            new { token = invitationEmail.Token });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var workspaces = await otherUser.Client.GetFromJsonAsync<List<WorkspaceResponse>>(
            "/api/workspaces");
        Assert.DoesNotContain(workspaces!, workspace => workspace.Id == workspaceId);
    }

    private async Task<RegisteredUser> RegisterAsync(string displayName, string? email = null)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });
        email ??= $"{displayName.Replace(" ", string.Empty).ToLowerInvariant()}-{Guid.NewGuid():N}@example.com";

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

    private sealed record RegisteredUser(Guid UserId, string Email, HttpClient Client);

    private sealed record AuthSessionResponse(AuthUserResponse User, string AccessToken);

    private sealed record AuthUserResponse(Guid Id);

    private sealed record WorkspaceResponse(
        Guid Id,
        string Name,
        string Role,
        int MemberCount);

    private sealed record ApiError(string Code);
}
