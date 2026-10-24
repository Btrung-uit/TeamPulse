using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeamPulse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGitHubPullRequestWaitingRoom : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalUrl",
                table: "Notifications",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceReference",
                table: "Notifications",
                type: "nvarchar(240)",
                maxLength: 240,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GitHubPullRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RepositoryFullName = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    Number = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Url = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    AuthorLogin = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RequestedReviewers = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CheckStatus = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    OpenedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    LastActivityAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GitHubPullRequests", x => x.Id);
                    table.UniqueConstraint("AK_GitHubPullRequests_WorkspaceId_ProjectId_Id", x => new { x.WorkspaceId, x.ProjectId, x.Id });
                    table.ForeignKey(
                        name: "FK_GitHubPullRequests_Projects_WorkspaceId_ProjectId",
                        columns: x => new { x.WorkspaceId, x.ProjectId },
                        principalTable: "Projects",
                        principalColumns: new[] { "WorkspaceId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GitHubPullRequestTasks",
                columns: table => new
                {
                    PullRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GitHubPullRequestTasks", x => new { x.PullRequestId, x.TaskId });
                    table.ForeignKey(
                        name: "FK_GitHubPullRequestTasks_GitHubPullRequests_WorkspaceId_ProjectId_PullRequestId",
                        columns: x => new { x.WorkspaceId, x.ProjectId, x.PullRequestId },
                        principalTable: "GitHubPullRequests",
                        principalColumns: new[] { "WorkspaceId", "ProjectId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GitHubPullRequestTasks_Tasks_WorkspaceId_ProjectId_TaskId",
                        columns: x => new { x.WorkspaceId, x.ProjectId, x.TaskId },
                        principalTable: "Tasks",
                        principalColumns: new[] { "WorkspaceId", "ProjectId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_WorkspaceId_RecipientUserId_SourceReference",
                table: "Notifications",
                columns: new[] { "WorkspaceId", "RecipientUserId", "SourceReference" },
                unique: true,
                filter: "[SourceReference] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_GitHubPullRequests_WorkspaceId_ProjectId_RepositoryFullName_Number",
                table: "GitHubPullRequests",
                columns: new[] { "WorkspaceId", "ProjectId", "RepositoryFullName", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GitHubPullRequests_WorkspaceId_ProjectId_Status_LastActivityAtUtc",
                table: "GitHubPullRequests",
                columns: new[] { "WorkspaceId", "ProjectId", "Status", "LastActivityAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_GitHubPullRequestTasks_WorkspaceId_ProjectId_PullRequestId",
                table: "GitHubPullRequestTasks",
                columns: new[] { "WorkspaceId", "ProjectId", "PullRequestId" });

            migrationBuilder.CreateIndex(
                name: "IX_GitHubPullRequestTasks_WorkspaceId_ProjectId_TaskId",
                table: "GitHubPullRequestTasks",
                columns: new[] { "WorkspaceId", "ProjectId", "TaskId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GitHubPullRequestTasks");

            migrationBuilder.DropTable(
                name: "GitHubPullRequests");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_WorkspaceId_RecipientUserId_SourceReference",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "ExternalUrl",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "SourceReference",
                table: "Notifications");
        }
    }
}
