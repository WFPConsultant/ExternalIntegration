using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UVP.ExternalIntegration.Repository.Migrations
{
    /// <inheritdoc />
    public partial class UpdatedFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "UpdatedUser",
                table: "IntegrationInvocations",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedOn",
                table: "IntegrationInvocations",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AddColumn<int>(
                name: "DoaCandidateId",
                table: "IntegrationInvocations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "IntegrationInvocations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<DateTime>(
                name: "RequestSentOn",
                table: "IntegrationInvocationLogs",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AddColumn<int>(
                name: "DoaCandidateId",
                table: "IntegrationInvocationLogs",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LogSequence",
                table: "IntegrationInvocationLogs",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationInvocations_DoaCandidateId",
                table: "IntegrationInvocations",
                column: "DoaCandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationInvocationLogs_DoaCandidateId",
                table: "IntegrationInvocationLogs",
                column: "DoaCandidateId");

            migrationBuilder.AddForeignKey(
                name: "FK_IntegrationInvocationLogs_DoaCandidates_DoaCandidateId",
                table: "IntegrationInvocationLogs",
                column: "DoaCandidateId",
                principalTable: "DoaCandidates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_IntegrationInvocations_DoaCandidates_DoaCandidateId",
                table: "IntegrationInvocations",
                column: "DoaCandidateId",
                principalTable: "DoaCandidates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_IntegrationInvocationLogs_DoaCandidates_DoaCandidateId",
                table: "IntegrationInvocationLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_IntegrationInvocations_DoaCandidates_DoaCandidateId",
                table: "IntegrationInvocations");

            migrationBuilder.DropIndex(
                name: "IX_IntegrationInvocations_DoaCandidateId",
                table: "IntegrationInvocations");

            migrationBuilder.DropIndex(
                name: "IX_IntegrationInvocationLogs_DoaCandidateId",
                table: "IntegrationInvocationLogs");

            migrationBuilder.DropColumn(
                name: "DoaCandidateId",
                table: "IntegrationInvocations");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "IntegrationInvocations");

            migrationBuilder.DropColumn(
                name: "DoaCandidateId",
                table: "IntegrationInvocationLogs");

            migrationBuilder.DropColumn(
                name: "LogSequence",
                table: "IntegrationInvocationLogs");

            migrationBuilder.AlterColumn<string>(
                name: "UpdatedUser",
                table: "IntegrationInvocations",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedOn",
                table: "IntegrationInvocations",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "RequestSentOn",
                table: "IntegrationInvocationLogs",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);
        }
    }
}
