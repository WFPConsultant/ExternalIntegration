using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UVP.ExternalIntegration.Repository.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Candidates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MiddleName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Gender = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CountryOfBirth = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CountryOfBirthISOCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    Nationality = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    NationalityISOCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    DateOfBirth = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Candidates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DoaCandidates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Department = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RequestorName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RequestorEmail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DoaCandidates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IntegrationEndpointConfigurations",
                columns: table => new
                {
                    IntegrationEndpointId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IntegrationType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IntegrationOperation = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BaseUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    PathTemplate = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    HttpMethod = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    TimeoutSeconds = table.Column<int>(type: "int", nullable: false),
                    MaxAttempts = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedUser = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUser = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SamplePayload = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SampleResponse = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UVPDataModel = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PayloadModelMapper = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Retrigger = table.Column<bool>(type: "bit", nullable: false),
                    RetriggerCount = table.Column<int>(type: "int", nullable: false),
                    RetriggerInterval = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationEndpointConfigurations", x => x.IntegrationEndpointId);
                });

            migrationBuilder.CreateTable(
                name: "IntegrationInvocations",
                columns: table => new
                {
                    IntegrationInvocationId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IntegrationType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IntegrationOperation = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IntegrationStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ReferenceId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    //ExternalReferenceId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    NextRetryTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedUser = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUser = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationInvocations", x => x.IntegrationInvocationId);
                });

            migrationBuilder.CreateTable(
                name: "DoaCandidateClearances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DoaCandidateId = table.Column<int>(type: "int", nullable: false),
                    RecruitmentClearanceCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RequestedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StatusCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    LinkDetailRemarks = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AdditionalRemarks = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompletionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Outcome = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DoaCandidateClearances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DoaCandidateClearances_DoaCandidates_DoaCandidateId",
                        column: x => x.DoaCandidateId,
                        principalTable: "DoaCandidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DoaCandidateClearancesOneHR",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DoaCandidateId = table.Column<int>(type: "int", nullable: false),
                    CandidateId = table.Column<int>(type: "int", nullable: false),
                    DoaCandidateClearanceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RVCaseId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RequestedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsCompleted = table.Column<bool>(type: "bit", nullable: false),
                    Retry = table.Column<int>(type: "int", nullable: false),
                    CompletionDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DoaCandidateClearancesOneHR", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DoaCandidateClearancesOneHR_Candidates_CandidateId",
                        column: x => x.CandidateId,
                        principalTable: "Candidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DoaCandidateClearancesOneHR_DoaCandidates_DoaCandidateId",
                        column: x => x.DoaCandidateId,
                        principalTable: "DoaCandidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IntegrationInvocationLogs",
                columns: table => new
                {
                    IntegrationInvocationLogId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IntegrationInvocationId = table.Column<long>(type: "bigint", nullable: false),
                    RequestPayload = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResponsePayload = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResponseStatusCode = table.Column<int>(type: "int", nullable: true),
                    IntegrationStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RequestSentOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResponseReceivedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResponseTimeMs = table.Column<long>(type: "bigint", nullable: true),
                    ErrorDetails = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedUser = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationInvocationLogs", x => x.IntegrationInvocationLogId);
                    table.ForeignKey(
                        name: "FK_IntegrationInvocationLogs_IntegrationInvocations_IntegrationInvocationId",
                        column: x => x.IntegrationInvocationId,
                        principalTable: "IntegrationInvocations",
                        principalColumn: "IntegrationInvocationId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DoaCandidateClearances_DoaCandidateId",
                table: "DoaCandidateClearances",
                column: "DoaCandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_DoaCandidateClearancesOneHR_CandidateId",
                table: "DoaCandidateClearancesOneHR",
                column: "CandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_DoaCandidateClearancesOneHR_DoaCandidateId_CandidateId",
                table: "DoaCandidateClearancesOneHR",
                columns: new[] { "DoaCandidateId", "CandidateId" });

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationEndpointConfigurations_IntegrationType_IntegrationOperation_IsActive",
                table: "IntegrationEndpointConfigurations",
                columns: new[] { "IntegrationType", "IntegrationOperation", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationInvocationLogs_IntegrationInvocationId",
                table: "IntegrationInvocationLogs",
                column: "IntegrationInvocationId");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationInvocations_IntegrationStatus",
                table: "IntegrationInvocations",
                column: "IntegrationStatus");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationInvocations_ReferenceId",
                table: "IntegrationInvocations",
                column: "ReferenceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DoaCandidateClearances");

            migrationBuilder.DropTable(
                name: "DoaCandidateClearancesOneHR");

            migrationBuilder.DropTable(
                name: "IntegrationEndpointConfigurations");

            migrationBuilder.DropTable(
                name: "IntegrationInvocationLogs");

            migrationBuilder.DropTable(
                name: "Candidates");

            migrationBuilder.DropTable(
                name: "DoaCandidates");

            migrationBuilder.DropTable(
                name: "IntegrationInvocations");
        }
    }
}
