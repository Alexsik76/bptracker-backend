using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BpTracker.Api.Migrations
{
    /// <inheritdoc />
    public partial class UpdateDateTimeOffsetTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Database-level NO-OP.
            // All target columns in the database are already 'timestamp with time zone' (timestamptz)
            // in the production database schema, so no ALTER COLUMN statements are necessary.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Database-level NO-OP.
        }
    }
}
