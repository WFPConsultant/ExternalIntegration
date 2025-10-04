using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UVP.ExternalIntegration.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddDoaEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            //migrationBuilder.DropIndex(
            //    name: "IX_IntegrationInvocations_ReferenceId",
            //    table: "IntegrationInvocations");

            //migrationBuilder.DropColumn(
            //    name: "DoaCandidateId",
            //    table: "IntegrationInvocations");

            //migrationBuilder.DropColumn(
            //    name: "ExternalReferenceId",
            //    table: "IntegrationInvocations");

            //migrationBuilder.DropColumn(
            //    name: "ReferenceId",
            //    table: "IntegrationInvocations");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            //migrationBuilder.AddColumn<int>(
            //    name: "DoaCandidateId",
            //    table: "IntegrationInvocations",
            //    type: "int",
            //    nullable: false,
            //    defaultValue: 0);

            //migrationBuilder.AddColumn<string>(
            //    name: "ExternalReferenceId",
            //    table: "IntegrationInvocations",
            //    type: "nvarchar(200)",
            //    maxLength: 200,
            //    nullable: true);

            //migrationBuilder.AddColumn<string>(
            //    name: "ReferenceId",
            //    table: "IntegrationInvocations",
            //    type: "nvarchar(200)",
            //    maxLength: 200,
            //    nullable: true);

            //migrationBuilder.CreateIndex(
            //    name: "IX_IntegrationInvocations_ReferenceId",
            //    table: "IntegrationInvocations",
            //    column: "ReferenceId");
        }
    }
}
