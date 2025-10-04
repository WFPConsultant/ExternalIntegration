using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UVP.ExternalIntegration.Repository.Migrations
{
    /// <inheritdoc />
    public partial class updatedOneHR : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DoaId",
                table: "DoaCandidateClearancesOneHR",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DoaId",
                table: "DoaCandidateClearancesOneHR");
        }
    }
}
