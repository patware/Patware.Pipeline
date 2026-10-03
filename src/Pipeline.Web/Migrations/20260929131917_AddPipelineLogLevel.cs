using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pipeline.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddPipelineLogLevel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Level",
                table: "PipelineRunLogs",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Level",
                table: "PipelineRunLogs");
        }
    }
}
