using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeamPulse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskDomain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_Sprints_WorkspaceId_ProjectId_Id",
                table: "Sprints",
                columns: new[] { "WorkspaceId", "ProjectId", "Id" });

            migrationBuilder.CreateTable(
                name: "Tasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SprintId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssigneeUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Code = table.Column<string>(type: "varchar(17)", unicode: false, maxLength: 17, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", maxLength: 5000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    StatusBeforeBlocked = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: true),
                    Priority = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    DueOn = table.Column<DateOnly>(type: "date", nullable: true),
                    EstimateHours = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: true),
                    HasStartedWorkflow = table.Column<bool>(type: "bit", nullable: false),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false),
                    ArchivedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tasks", x => x.Id);
                    table.UniqueConstraint("AK_Tasks_WorkspaceId_ProjectId_Id", x => new { x.WorkspaceId, x.ProjectId, x.Id });
                    table.CheckConstraint("CK_Tasks_ArchiveState", "([IsArchived] = 0 AND [ArchivedAtUtc] IS NULL) OR ([IsArchived] = 1 AND [ArchivedAtUtc] IS NOT NULL)");
                    table.CheckConstraint("CK_Tasks_BlockedState", "([Status] = 'Blocked' AND [StatusBeforeBlocked] IS NOT NULL) OR ([Status] <> 'Blocked' AND [StatusBeforeBlocked] IS NULL)");
                    table.CheckConstraint("CK_Tasks_EstimateHours", "[EstimateHours] IS NULL OR ([EstimateHours] > 0 AND [EstimateHours] <= 9999.99)");
                    table.CheckConstraint("CK_Tasks_Priority", "[Priority] IN ('Low', 'Medium', 'High', 'Urgent')");
                    table.CheckConstraint("CK_Tasks_Status", "[Status] IN ('Todo', 'InProgress', 'EvidenceLinked', 'InReview', 'NeedsChanges', 'Blocked', 'Done')");
                    table.ForeignKey(
                        name: "FK_Tasks_Projects_WorkspaceId_ProjectId",
                        columns: x => new { x.WorkspaceId, x.ProjectId },
                        principalTable: "Projects",
                        principalColumns: new[] { "WorkspaceId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Tasks_Sprints_WorkspaceId_ProjectId_SprintId",
                        columns: x => new { x.WorkspaceId, x.ProjectId, x.SprintId },
                        principalTable: "Sprints",
                        principalColumns: new[] { "WorkspaceId", "ProjectId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tasks_WorkspaceMembers_WorkspaceId_AssigneeUserId",
                        columns: x => new { x.WorkspaceId, x.AssigneeUserId },
                        principalTable: "WorkspaceMembers",
                        principalColumns: new[] { "WorkspaceId", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tasks_WorkspaceMembers_WorkspaceId_CreatedByUserId",
                        columns: x => new { x.WorkspaceId, x.CreatedByUserId },
                        principalTable: "WorkspaceMembers",
                        principalColumns: new[] { "WorkspaceId", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tasks_WorkspaceMembers_WorkspaceId_ReviewerUserId",
                        columns: x => new { x.WorkspaceId, x.ReviewerUserId },
                        principalTable: "WorkspaceMembers",
                        principalColumns: new[] { "WorkspaceId", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TaskDependencies",
                columns: table => new
                {
                    WorkspaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PredecessorTaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SuccessorTaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskDependencies", x => new { x.WorkspaceId, x.ProjectId, x.PredecessorTaskId, x.SuccessorTaskId });
                    table.CheckConstraint("CK_TaskDependencies_DifferentTasks", "[PredecessorTaskId] <> [SuccessorTaskId]");
                    table.ForeignKey(
                        name: "FK_TaskDependencies_Tasks_WorkspaceId_ProjectId_PredecessorTaskId",
                        columns: x => new { x.WorkspaceId, x.ProjectId, x.PredecessorTaskId },
                        principalTable: "Tasks",
                        principalColumns: new[] { "WorkspaceId", "ProjectId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaskDependencies_Tasks_WorkspaceId_ProjectId_SuccessorTaskId",
                        columns: x => new { x.WorkspaceId, x.ProjectId, x.SuccessorTaskId },
                        principalTable: "Tasks",
                        principalColumns: new[] { "WorkspaceId", "ProjectId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TaskDependencies_WorkspaceId_ProjectId_SuccessorTaskId",
                table: "TaskDependencies",
                columns: new[] { "WorkspaceId", "ProjectId", "SuccessorTaskId" });

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_WorkspaceId_AssigneeUserId_Status",
                table: "Tasks",
                columns: new[] { "WorkspaceId", "AssigneeUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_WorkspaceId_CreatedByUserId",
                table: "Tasks",
                columns: new[] { "WorkspaceId", "CreatedByUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_WorkspaceId_ProjectId_Code",
                table: "Tasks",
                columns: new[] { "WorkspaceId", "ProjectId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_WorkspaceId_ProjectId_IsArchived_Status_DueOn",
                table: "Tasks",
                columns: new[] { "WorkspaceId", "ProjectId", "IsArchived", "Status", "DueOn" });

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_WorkspaceId_ProjectId_SprintId",
                table: "Tasks",
                columns: new[] { "WorkspaceId", "ProjectId", "SprintId" });

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_WorkspaceId_ReviewerUserId",
                table: "Tasks",
                columns: new[] { "WorkspaceId", "ReviewerUserId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TaskDependencies");

            migrationBuilder.DropTable(
                name: "Tasks");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Sprints_WorkspaceId_ProjectId_Id",
                table: "Sprints");
        }
    }
}
