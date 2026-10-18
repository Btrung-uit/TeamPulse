using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeamPulse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectResources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectResources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Url = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    ExternalId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LinkedTaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectResources", x => x.Id);
                    table.UniqueConstraint("AK_ProjectResources_WorkspaceId_ProjectId_Id", x => new { x.WorkspaceId, x.ProjectId, x.Id });
                    table.ForeignKey(
                        name: "FK_ProjectResources_Projects_WorkspaceId_ProjectId",
                        columns: x => new { x.WorkspaceId, x.ProjectId },
                        principalTable: "Projects",
                        principalColumns: new[] { "WorkspaceId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectResources_Tasks_WorkspaceId_ProjectId_LinkedTaskId",
                        columns: x => new { x.WorkspaceId, x.ProjectId, x.LinkedTaskId },
                        principalTable: "Tasks",
                        principalColumns: new[] { "WorkspaceId", "ProjectId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectResources_WorkspaceMembers_WorkspaceId_OwnerUserId",
                        columns: x => new { x.WorkspaceId, x.OwnerUserId },
                        principalTable: "WorkspaceMembers",
                        principalColumns: new[] { "WorkspaceId", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectResources_WorkspaceId_OwnerUserId",
                table: "ProjectResources",
                columns: new[] { "WorkspaceId", "OwnerUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectResources_WorkspaceId_ProjectId_LinkedTaskId",
                table: "ProjectResources",
                columns: new[] { "WorkspaceId", "ProjectId", "LinkedTaskId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectResources_WorkspaceId_ProjectId_Type_UpdatedAtUtc",
                table: "ProjectResources",
                columns: new[] { "WorkspaceId", "ProjectId", "Type", "UpdatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectResources");
        }
    }
}
