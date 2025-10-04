using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UVP.ExternalIntegration.Repository.Migrations
{
    /// <inheritdoc />
    public partial class FieldChange : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            //migrationBuilder.DropForeignKey(
            //    name: "FK_IntegrationInvocations_DoaCandidates_DoaCandidateId",
            //    table: "IntegrationInvocations");

            //migrationBuilder.DropIndex(
            //    name: "IX_IntegrationInvocations_DoaCandidateId",
            //    table: "IntegrationInvocations");

            //migrationBuilder.DropIndex(
            //    name: "IX_IntegrationInvocationLogs_DoaCandidateId",
            //    table: "IntegrationInvocationLogs");

            migrationBuilder.DropColumn(
                name: "DoaCandidateId",
                table: "IntegrationInvocationLogs");

            migrationBuilder.AlterColumn<string>(
                name: "ReferenceId",
                table: "IntegrationInvocations",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ReferenceId",
                table: "IntegrationInvocations",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DoaCandidateId",
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
                name: "FK_IntegrationInvocations_DoaCandidates_DoaCandidateId",
                table: "IntegrationInvocations",
                column: "DoaCandidateId",
                principalTable: "DoaCandidates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
