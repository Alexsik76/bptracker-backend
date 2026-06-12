using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BpTracker.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUserOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "TreatmentSchemas",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "ReminderTemplates",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "IntakeReports",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "EmailOutbox",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE ""TreatmentSchemas""
                SET ""UserId"" = 'e6915d20-30fd-4154-b43a-85c04dbac190'::uuid
                WHERE ""UserId"" IS NULL;
            ");

            migrationBuilder.Sql(@"
                UPDATE ""ReminderTemplates""
                SET ""UserId"" = 'e6915d20-30fd-4154-b43a-85c04dbac190'::uuid
                WHERE ""UserId"" IS NULL;
            ");

            migrationBuilder.Sql(@"
                UPDATE ""IntakeReports""
                SET ""UserId"" = 'e6915d20-30fd-4154-b43a-85c04dbac190'::uuid
                WHERE ""UserId"" IS NULL;
            ");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "TreatmentSchemas",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "ReminderTemplates",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "IntakeReports",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentSchemas_UserId",
                table: "TreatmentSchemas",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReminderTemplates_UserId",
                table: "ReminderTemplates",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_IntakeReports_TemplateId",
                table: "IntakeReports",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailOutbox_UserId",
                table: "EmailOutbox",
                column: "UserId");

            migrationBuilder.DropIndex(
                name: "IX_IntakeReports_TemplateId_Period_Date",
                table: "IntakeReports");

            migrationBuilder.CreateIndex(
                name: "IX_IntakeReports_UserId_TemplateId_Period_Date",
                table: "IntakeReports",
                columns: new[] { "UserId", "TemplateId", "Period", "Date" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_IntakeReports_Users_UserId",
                table: "IntakeReports",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ReminderTemplates_Users_UserId",
                table: "ReminderTemplates",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TreatmentSchemas_Users_UserId",
                table: "TreatmentSchemas",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_EmailOutbox_Users_UserId",
                table: "EmailOutbox",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EmailOutbox_Users_UserId",
                table: "EmailOutbox");

            migrationBuilder.DropForeignKey(
                name: "FK_IntakeReports_Users_UserId",
                table: "IntakeReports");

            migrationBuilder.DropForeignKey(
                name: "FK_ReminderTemplates_Users_UserId",
                table: "ReminderTemplates");

            migrationBuilder.DropForeignKey(
                name: "FK_TreatmentSchemas_Users_UserId",
                table: "TreatmentSchemas");

            migrationBuilder.DropIndex(
                name: "IX_TreatmentSchemas_UserId",
                table: "TreatmentSchemas");

            migrationBuilder.DropIndex(
                name: "IX_ReminderTemplates_UserId",
                table: "ReminderTemplates");

            migrationBuilder.DropIndex(
                name: "IX_IntakeReports_TemplateId",
                table: "IntakeReports");

            migrationBuilder.DropIndex(
                name: "IX_EmailOutbox_UserId",
                table: "EmailOutbox");

            migrationBuilder.DropIndex(
                name: "IX_IntakeReports_UserId_TemplateId_Period_Date",
                table: "IntakeReports");

            migrationBuilder.CreateIndex(
                name: "IX_IntakeReports_TemplateId_Period_Date",
                table: "IntakeReports",
                columns: new[] { "TemplateId", "Period", "Date" },
                unique: true);

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "TreatmentSchemas");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "ReminderTemplates");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "IntakeReports");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "EmailOutbox");
        }
    }
}
