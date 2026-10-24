using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeamPulse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGitHubEvidenceAutomation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "SubmittedByUserId",
                table: "TaskEvidence",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "TaskEvidence",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "TaskEvidence",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Manual");

            migrationBuilder.AlterColumn<Guid>(
                name: "ActorUserId",
                table: "ActivityLogs",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "ActivityLogs",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "User");

            migrationBuilder.AddColumn<string>(
                name: "SourceReference",
                table: "ActivityLogs",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaskEvidence_WorkspaceId_ProjectId_TaskId_Source_ExternalId",
                table: "TaskEvidence",
                columns: new[] { "WorkspaceId", "ProjectId", "TaskId", "Source", "ExternalId" },
                unique: true,
                filter: "[ExternalId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogs_SourceReference",
                table: "ActivityLogs",
                column: "SourceReference");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TaskEvidence_WorkspaceId_ProjectId_TaskId_Source_ExternalId",
                table: "TaskEvidence");

            migrationBuilder.DropIndex(
                name: "IX_ActivityLogs_SourceReference",
                table: "ActivityLogs");

            migrationBuilder.Sql(
                "DELETE FROM [TaskEvidence] WHERE [SubmittedByUserId] IS NULL;");
            migrationBuilder.Sql(
                "DELETE FROM [ActivityLogs] WHERE [ActorUserId] IS NULL;");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "TaskEvidence");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "TaskEvidence");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "ActivityLogs");

            migrationBuilder.DropColumn(
                name: "SourceReference",
                table: "ActivityLogs");

            migrationBuilder.AlterColumn<Guid>(
                name: "SubmittedByUserId",
                table: "TaskEvidence",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ActorUserId",
                table: "ActivityLogs",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);
        }
    }
}
