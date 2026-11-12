using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MS.Cobroup.Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOpenedFieldsToRouteDayClosings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "OpenedAtUtc",
                schema: "operations",
                table: "route_day_closings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OpenedByUserId",
                schema: "operations",
                table: "route_day_closings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OpenedByDisplayName",
                schema: "operations",
                table: "route_day_closings",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OpenedAtUtc",
                schema: "operations",
                table: "route_day_closings");

            migrationBuilder.DropColumn(
                name: "OpenedByUserId",
                schema: "operations",
                table: "route_day_closings");

            migrationBuilder.DropColumn(
                name: "OpenedByDisplayName",
                schema: "operations",
                table: "route_day_closings");
        }
    }
}

