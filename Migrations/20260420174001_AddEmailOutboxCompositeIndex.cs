using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BpTracker.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailOutboxCompositeIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EmailOutbox_NextAttemptAt",
                table: "EmailOutbox");

            migrationBuilder.DropIndex(
                name: "IX_EmailOutbox_Status",
                table: "EmailOutbox");

            migrationBuilder.CreateIndex(
                name: "IX_EmailOutbox_Status_NextAttemptAt",
                table: "EmailOutbox",
                columns: new[] { "Status", "NextAttemptAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EmailOutbox_Status_NextAttemptAt",
                table: "EmailOutbox");

            migrationBuilder.CreateIndex(
                name: "IX_EmailOutbox_NextAttemptAt",
                table: "EmailOutbox",
                column: "NextAttemptAt");

            migrationBuilder.CreateIndex(
                name: "IX_EmailOutbox_Status",
                table: "EmailOutbox",
                column: "Status");
        }
    }
}
