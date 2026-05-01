using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BpTracker.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUserLastExportAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastExportAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastExportAt",
                table: "Users");
        }
    }
}
