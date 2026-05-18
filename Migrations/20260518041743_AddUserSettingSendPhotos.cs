using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BpTracker.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUserSettingSendPhotos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SendPhotos",
                table: "UserSettings",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SendPhotos",
                table: "UserSettings");
        }
    }
}
