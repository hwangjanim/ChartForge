using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChartForge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSummaryAndSuggestedActions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SuggestedActionsJson",
                table: "ChartStates");

            migrationBuilder.DropColumn(
                name: "SummaryMetricsJson",
                table: "ChartStates");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SuggestedActionsJson",
                table: "ChartStates",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SummaryMetricsJson",
                table: "ChartStates",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
