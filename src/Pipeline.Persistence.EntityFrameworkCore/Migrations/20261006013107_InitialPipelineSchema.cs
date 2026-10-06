using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pipeline.Persistence.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class InitialPipelineSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "pipeline");

            migrationBuilder.CreateTable(
                name: "PipelineRuns",
                schema: "pipeline",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DefinitionId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DefinitionDisplayName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    DefinitionVersion = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    QueuedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FinishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    StatusText = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PipelineRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PipelineJobs",
                schema: "pipeline",
                columns: table => new
                {
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    ConditionResult = table.Column<bool>(type: "bit", nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FinishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PipelineJobs", x => new { x.RunId, x.JobId });
                    table.ForeignKey(
                        name: "FK_PipelineJobs_PipelineRuns_RunId",
                        column: x => x.RunId,
                        principalSchema: "pipeline",
                        principalTable: "PipelineRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PipelineRunLogs",
                schema: "pipeline",
                columns: table => new
                {
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Level = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    JobId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    StepId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PipelineRunLogs", x => new { x.RunId, x.Sequence });
                    table.ForeignKey(
                        name: "FK_PipelineRunLogs_PipelineRuns_RunId",
                        column: x => x.RunId,
                        principalSchema: "pipeline",
                        principalTable: "PipelineRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PipelineSpecifications",
                schema: "pipeline",
                columns: table => new
                {
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DefinitionId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DefinitionVersion = table.Column<int>(type: "int", nullable: false),
                    InputJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SettingsJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PipelineSpecifications", x => x.RunId);
                    table.ForeignKey(
                        name: "FK_PipelineSpecifications_PipelineRuns_RunId",
                        column: x => x.RunId,
                        principalSchema: "pipeline",
                        principalTable: "PipelineRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PipelineSteps",
                schema: "pipeline",
                columns: table => new
                {
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    StepId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    Attempt = table.Column<int>(type: "int", nullable: false),
                    ArgumentsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HasOutput = table.Column<bool>(type: "bit", nullable: false),
                    OutputJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FinishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PollDeadline = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LeaseToken = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LeaseExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PipelineSteps", x => new { x.RunId, x.JobId, x.StepId });
                    table.ForeignKey(
                        name: "FK_PipelineSteps_PipelineJobs_RunId_JobId",
                        columns: x => new { x.RunId, x.JobId },
                        principalSchema: "pipeline",
                        principalTable: "PipelineJobs",
                        principalColumns: new[] { "RunId", "JobId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PipelineRuns_CreatedAt_Id",
                schema: "pipeline",
                table: "PipelineRuns",
                columns: new[] { "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_PipelineRuns_Status_QueuedAt_Id",
                schema: "pipeline",
                table: "PipelineRuns",
                columns: new[] { "Status", "QueuedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_PipelineSteps_Status_NextAttemptAt",
                schema: "pipeline",
                table: "PipelineSteps",
                columns: new[] { "Status", "NextAttemptAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PipelineRunLogs",
                schema: "pipeline");

            migrationBuilder.DropTable(
                name: "PipelineSpecifications",
                schema: "pipeline");

            migrationBuilder.DropTable(
                name: "PipelineSteps",
                schema: "pipeline");

            migrationBuilder.DropTable(
                name: "PipelineJobs",
                schema: "pipeline");

            migrationBuilder.DropTable(
                name: "PipelineRuns",
                schema: "pipeline");
        }
    }
}
