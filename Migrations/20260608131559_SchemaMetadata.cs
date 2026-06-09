using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BpTracker.Api.Migrations
{
    /// <inheritdoc />
    public partial class SchemaMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "TreatmentSchemas" DROP CONSTRAINT "PK_TreatmentSchemas";
                ALTER TABLE "TreatmentSchemas" ALTER COLUMN "Id" DROP DEFAULT;
                ALTER TABLE "TreatmentSchemas"
                  ALTER COLUMN "Id" TYPE uuid USING gen_random_uuid();
                ALTER TABLE "TreatmentSchemas" ADD CONSTRAINT "PK_TreatmentSchemas" PRIMARY KEY ("Id");
                """);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "TreatmentSchemas",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<string>(
                name: "Doctor",
                table: "TreatmentSchemas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PrescribedOn",
                table: "TreatmentSchemas",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "TreatmentSchemas");

            migrationBuilder.DropColumn(
                name: "Doctor",
                table: "TreatmentSchemas");

            migrationBuilder.DropColumn(
                name: "PrescribedOn",
                table: "TreatmentSchemas");

            migrationBuilder.AlterColumn<string>(
                name: "Id",
                table: "TreatmentSchemas",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid");
        }
    }
}
