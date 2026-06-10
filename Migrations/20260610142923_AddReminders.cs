using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BpTracker.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReminderTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SchemaId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    MaxReminders = table.Column<int>(type: "integer", nullable: false),
                    Periods = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReminderTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReminderTemplates_TreatmentSchemas_SchemaId",
                        column: x => x.SchemaId,
                        principalTable: "TreatmentSchemas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IntakeReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Period = table.Column<string>(type: "text", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntakeReports_ReminderTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "ReminderTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IntakeReports_TemplateId_Period_Date",
                table: "IntakeReports",
                columns: new[] { "TemplateId", "Period", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReminderTemplates_SchemaId",
                table: "ReminderTemplates",
                column: "SchemaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IntakeReports");

            migrationBuilder.DropTable(
                name: "ReminderTemplates");
        }
    }
}
