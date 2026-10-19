using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeamPulse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConfirmTaskImport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ConfirmedAtUtc",
                table: "ImportBatches",
                type: "datetimeoffset(7)",
                precision: 7,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConfirmedByUserId",
                table: "ImportBatches",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ImportedTaskCount",
                table: "ImportBatches",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ImportBatches",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.CreateIndex(
                name: "IX_ImportBatches_WorkspaceId_ConfirmedByUserId",
                table: "ImportBatches",
                columns: new[] { "WorkspaceId", "ConfirmedByUserId" });

            migrationBuilder.AddForeignKey(
                name: "FK_ImportBatches_WorkspaceMembers_WorkspaceId_ConfirmedByUserId",
                table: "ImportBatches",
                columns: new[] { "WorkspaceId", "ConfirmedByUserId" },
                principalTable: "WorkspaceMembers",
                principalColumns: new[] { "WorkspaceId", "UserId" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ImportBatches_WorkspaceMembers_WorkspaceId_ConfirmedByUserId",
                table: "ImportBatches");

            migrationBuilder.DropIndex(
                name: "IX_ImportBatches_WorkspaceId_ConfirmedByUserId",
                table: "ImportBatches");

            migrationBuilder.DropColumn(
                name: "ConfirmedAtUtc",
                table: "ImportBatches");

            migrationBuilder.DropColumn(
                name: "ConfirmedByUserId",
                table: "ImportBatches");

            migrationBuilder.DropColumn(
                name: "ImportedTaskCount",
                table: "ImportBatches");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ImportBatches");
        }
    }
}
