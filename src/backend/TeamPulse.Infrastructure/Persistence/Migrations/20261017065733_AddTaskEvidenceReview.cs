using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeamPulse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskEvidenceReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TaskComments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskComments_Tasks_WorkspaceId_ProjectId_TaskId",
                        columns: x => new { x.WorkspaceId, x.ProjectId, x.TaskId },
                        principalTable: "Tasks",
                        principalColumns: new[] { "WorkspaceId", "ProjectId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TaskComments_WorkspaceMembers_WorkspaceId_AuthorUserId",
                        columns: x => new { x.WorkspaceId, x.AuthorUserId },
                        principalTable: "WorkspaceMembers",
                        principalColumns: new[] { "WorkspaceId", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TaskEvidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmittedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    Url = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskEvidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskEvidence_Tasks_WorkspaceId_ProjectId_TaskId",
                        columns: x => new { x.WorkspaceId, x.ProjectId, x.TaskId },
                        principalTable: "Tasks",
                        principalColumns: new[] { "WorkspaceId", "ProjectId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TaskEvidence_WorkspaceMembers_WorkspaceId_SubmittedByUserId",
                        columns: x => new { x.WorkspaceId, x.SubmittedByUserId },
                        principalTable: "WorkspaceMembers",
                        principalColumns: new[] { "WorkspaceId", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TaskComments_WorkspaceId_AuthorUserId",
                table: "TaskComments",
                columns: new[] { "WorkspaceId", "AuthorUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskComments_WorkspaceId_ProjectId_TaskId_CreatedAtUtc",
                table: "TaskComments",
                columns: new[] { "WorkspaceId", "ProjectId", "TaskId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskEvidence_WorkspaceId_ProjectId_TaskId_CreatedAtUtc",
                table: "TaskEvidence",
                columns: new[] { "WorkspaceId", "ProjectId", "TaskId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskEvidence_WorkspaceId_SubmittedByUserId",
                table: "TaskEvidence",
                columns: new[] { "WorkspaceId", "SubmittedByUserId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TaskComments");

            migrationBuilder.DropTable(
                name: "TaskEvidence");
        }
    }
}
