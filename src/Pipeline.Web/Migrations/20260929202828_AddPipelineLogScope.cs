using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pipeline.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddPipelineLogScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "JobId",
                table: "PipelineRunLogs",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StepId",
                table: "PipelineRunLogs",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "JobId",
                table: "PipelineRunLogs");

            migrationBuilder.DropColumn(
                name: "StepId",
                table: "PipelineRunLogs");
        }
    }
}
