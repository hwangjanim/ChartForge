using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChartForge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddJsonData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "JsonData",
                table: "DataStates",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "JsonData",
                table: "DataStates");
        }
    }
}
