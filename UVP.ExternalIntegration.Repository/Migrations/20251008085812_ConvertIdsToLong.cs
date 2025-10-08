using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UVP.ExternalIntegration.Repository.Migrations
{
    /// <inheritdoc />
    public partial class ConvertIdsToLong : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Drop all foreign key constraints
            migrationBuilder.DropForeignKey(
                name: "FK_DoaCandidateClearances_DoaCandidates_DoaCandidateId",
                table: "DoaCandidateClearances");

            migrationBuilder.DropForeignKey(
                name: "FK_DoaCandidateClearancesOneHR_Candidates_CandidateId",
                table: "DoaCandidateClearancesOneHR");

            migrationBuilder.DropForeignKey(
                name: "FK_DoaCandidateClearancesOneHR_DoaCandidates_DoaCandidateId",
                table: "DoaCandidateClearancesOneHR");

            // Step 2: Drop all indexes
            migrationBuilder.DropIndex(
                name: "IX_DoaCandidateClearancesOneHR_CandidateId",
                table: "DoaCandidateClearancesOneHR");

            migrationBuilder.DropIndex(
                name: "IX_DoaCandidateClearancesOneHR_DoaCandidateId_CandidateId",
                table: "DoaCandidateClearancesOneHR");

            migrationBuilder.DropIndex(
                name: "IX_DoaCandidateClearances_DoaCandidateId",
                table: "DoaCandidateClearances");

            // Step 3: Drop Primary Keys
            migrationBuilder.DropPrimaryKey(name: "PK_Users", table: "Users");
            migrationBuilder.DropPrimaryKey(name: "PK_Doas", table: "Doas");
            migrationBuilder.DropPrimaryKey(name: "PK_Candidates", table: "Candidates");
            migrationBuilder.DropPrimaryKey(name: "PK_DoaCandidates", table: "DoaCandidates");
            migrationBuilder.DropPrimaryKey(name: "PK_DoaCandidateClearances", table: "DoaCandidateClearances");
            migrationBuilder.DropPrimaryKey(name: "PK_DoaCandidateClearancesOneHR", table: "DoaCandidateClearancesOneHR");
            migrationBuilder.DropPrimaryKey(name: "PK_IntegrationEndpointConfigurations", table: "IntegrationEndpointConfigurations");

            // Step 4: Alter columns to bigint
            migrationBuilder.AlterColumn<long>(
                name: "Id",
                table: "Users",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<long>(
                name: "Id",
                table: "Doas",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<long>(
                name: "Id",
                table: "Candidates",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<long>(
                name: "UserId",
                table: "Candidates",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<long>(
                name: "Id",
                table: "DoaCandidates",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<long>(
                name: "DoaId",
                table: "DoaCandidates",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<long>(
                name: "CandidateId",
                table: "DoaCandidates",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<long>(
                name: "Id",
                table: "DoaCandidateClearances",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<long>(
                name: "DoaCandidateId",
                table: "DoaCandidateClearances",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<long>(
                name: "Id",
                table: "DoaCandidateClearancesOneHR",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<long>(
                name: "DoaId",
                table: "DoaCandidateClearancesOneHR",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<long>(
                name: "DoaCandidateId",
                table: "DoaCandidateClearancesOneHR",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<long>(
                name: "CandidateId",
                table: "DoaCandidateClearancesOneHR",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<long>(
                name: "IntegrationEndpointId",
                table: "IntegrationEndpointConfigurations",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            // Step 5: Recreate Primary Keys
            migrationBuilder.AddPrimaryKey(name: "PK_Users", table: "Users", column: "Id");
            migrationBuilder.AddPrimaryKey(name: "PK_Doas", table: "Doas", column: "Id");
            migrationBuilder.AddPrimaryKey(name: "PK_Candidates", table: "Candidates", column: "Id");
            migrationBuilder.AddPrimaryKey(name: "PK_DoaCandidates", table: "DoaCandidates", column: "Id");
            migrationBuilder.AddPrimaryKey(name: "PK_DoaCandidateClearances", table: "DoaCandidateClearances", column: "Id");
            migrationBuilder.AddPrimaryKey(name: "PK_DoaCandidateClearancesOneHR", table: "DoaCandidateClearancesOneHR", column: "Id");
            migrationBuilder.AddPrimaryKey(name: "PK_IntegrationEndpointConfigurations", table: "IntegrationEndpointConfigurations", column: "IntegrationEndpointId");

            // Step 6: Recreate indexes
            migrationBuilder.CreateIndex(
                name: "IX_DoaCandidateClearancesOneHR_CandidateId",
                table: "DoaCandidateClearancesOneHR",
                column: "CandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_DoaCandidateClearancesOneHR_DoaCandidateId_CandidateId",
                table: "DoaCandidateClearancesOneHR",
                columns: new[] { "DoaCandidateId", "CandidateId" });

            migrationBuilder.CreateIndex(
                name: "IX_DoaCandidateClearances_DoaCandidateId",
                table: "DoaCandidateClearances",
                column: "DoaCandidateId");

            // Step 7: Recreate foreign key constraints
            migrationBuilder.AddForeignKey(
                name: "FK_DoaCandidateClearances_DoaCandidates_DoaCandidateId",
                table: "DoaCandidateClearances",
                column: "DoaCandidateId",
                principalTable: "DoaCandidates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DoaCandidateClearancesOneHR_Candidates_CandidateId",
                table: "DoaCandidateClearancesOneHR",
                column: "CandidateId",
                principalTable: "Candidates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DoaCandidateClearancesOneHR_DoaCandidates_DoaCandidateId",
                table: "DoaCandidateClearancesOneHR",
                column: "DoaCandidateId",
                principalTable: "DoaCandidates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop foreign keys
            migrationBuilder.DropForeignKey(
                name: "FK_DoaCandidateClearances_DoaCandidates_DoaCandidateId",
                table: "DoaCandidateClearances");

            migrationBuilder.DropForeignKey(
                name: "FK_DoaCandidateClearancesOneHR_Candidates_CandidateId",
                table: "DoaCandidateClearancesOneHR");

            migrationBuilder.DropForeignKey(
                name: "FK_DoaCandidateClearancesOneHR_DoaCandidates_DoaCandidateId",
                table: "DoaCandidateClearancesOneHR");

            // Drop indexes
            migrationBuilder.DropIndex(
                name: "IX_DoaCandidateClearancesOneHR_CandidateId",
                table: "DoaCandidateClearancesOneHR");

            migrationBuilder.DropIndex(
                name: "IX_DoaCandidateClearancesOneHR_DoaCandidateId_CandidateId",
                table: "DoaCandidateClearancesOneHR");

            migrationBuilder.DropIndex(
                name: "IX_DoaCandidateClearances_DoaCandidateId",
                table: "DoaCandidateClearances");

            // Drop Primary Keys
            migrationBuilder.DropPrimaryKey(name: "PK_Users", table: "Users");
            migrationBuilder.DropPrimaryKey(name: "PK_Doas", table: "Doas");
            migrationBuilder.DropPrimaryKey(name: "PK_Candidates", table: "Candidates");
            migrationBuilder.DropPrimaryKey(name: "PK_DoaCandidates", table: "DoaCandidates");
            migrationBuilder.DropPrimaryKey(name: "PK_DoaCandidateClearances", table: "DoaCandidateClearances");
            migrationBuilder.DropPrimaryKey(name: "PK_DoaCandidateClearancesOneHR", table: "DoaCandidateClearancesOneHR");
            migrationBuilder.DropPrimaryKey(name: "PK_IntegrationEndpointConfigurations", table: "IntegrationEndpointConfigurations");

            // Revert columns back to int
            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Users",
                type: "int",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Doas",
                type: "int",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Candidates",
                type: "int",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<int>(
                name: "UserId",
                table: "Candidates",
                type: "int",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "DoaCandidates",
                type: "int",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<int>(
                name: "DoaId",
                table: "DoaCandidates",
                type: "int",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<int>(
                name: "CandidateId",
                table: "DoaCandidates",
                type: "int",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "DoaCandidateClearances",
                type: "int",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<int>(
                name: "DoaCandidateId",
                table: "DoaCandidateClearances",
                type: "int",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "DoaCandidateClearancesOneHR",
                type: "int",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<int>(
                name: "DoaId",
                table: "DoaCandidateClearancesOneHR",
                type: "int",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<int>(
                name: "DoaCandidateId",
                table: "DoaCandidateClearancesOneHR",
                type: "int",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<int>(
                name: "CandidateId",
                table: "DoaCandidateClearancesOneHR",
                type: "int",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<int>(
                name: "IntegrationEndpointId",
                table: "IntegrationEndpointConfigurations",
                type: "int",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            // Recreate Primary Keys
            migrationBuilder.AddPrimaryKey(name: "PK_Users", table: "Users", column: "Id");
            migrationBuilder.AddPrimaryKey(name: "PK_Doas", table: "Doas", column: "Id");
            migrationBuilder.AddPrimaryKey(name: "PK_Candidates", table: "Candidates", column: "Id");
            migrationBuilder.AddPrimaryKey(name: "PK_DoaCandidates", table: "DoaCandidates", column: "Id");
            migrationBuilder.AddPrimaryKey(name: "PK_DoaCandidateClearances", table: "DoaCandidateClearances", column: "Id");
            migrationBuilder.AddPrimaryKey(name: "PK_DoaCandidateClearancesOneHR", table: "DoaCandidateClearancesOneHR", column: "Id");
            migrationBuilder.AddPrimaryKey(name: "PK_IntegrationEndpointConfigurations", table: "IntegrationEndpointConfigurations", column: "IntegrationEndpointId");

            // Recreate indexes
            migrationBuilder.CreateIndex(
                name: "IX_DoaCandidateClearancesOneHR_CandidateId",
                table: "DoaCandidateClearancesOneHR",
                column: "CandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_DoaCandidateClearancesOneHR_DoaCandidateId_CandidateId",
                table: "DoaCandidateClearancesOneHR",
                columns: new[] { "DoaCandidateId", "CandidateId" });

            migrationBuilder.CreateIndex(
                name: "IX_DoaCandidateClearances_DoaCandidateId",
                table: "DoaCandidateClearances",
                column: "DoaCandidateId");

            // Recreate foreign keys
            migrationBuilder.AddForeignKey(
                name: "FK_DoaCandidateClearances_DoaCandidates_DoaCandidateId",
                table: "DoaCandidateClearances",
                column: "DoaCandidateId",
                principalTable: "DoaCandidates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DoaCandidateClearancesOneHR_Candidates_CandidateId",
                table: "DoaCandidateClearancesOneHR",
                column: "CandidateId",
                principalTable: "Candidates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DoaCandidateClearancesOneHR_DoaCandidates_DoaCandidateId",
                table: "DoaCandidateClearancesOneHR",
                column: "DoaCandidateId",
                principalTable: "DoaCandidates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
