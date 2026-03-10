using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChartForge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CleanupUnusedProperties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Messages_ChartStates_ChartStateId",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Messages_ChartStateId",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "AvatarUrl",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ChartStateId",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "ChartTypeIcon",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "ChartLibrary",
                table: "ChartStates");

            migrationBuilder.DropColumn(
                name: "MessageId",
                table: "ChartStates");

            migrationBuilder.DropColumn(
                name: "VersionLabel",
                table: "ChartStates");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AvatarUrl",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ChartStateId",
                table: "Messages",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChartTypeIcon",
                table: "Conversations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ChartLibrary",
                table: "ChartStates",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MessageId",
                table: "ChartStates",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VersionLabel",
                table: "ChartStates",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ChartStateId",
                table: "Messages",
                column: "ChartStateId",
                unique: true,
                filter: "[ChartStateId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_ChartStates_ChartStateId",
                table: "Messages",
                column: "ChartStateId",
                principalTable: "ChartStates",
                principalColumn: "Id");
        }
    }
}
