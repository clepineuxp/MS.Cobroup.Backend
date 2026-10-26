using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MS.Cobroup.Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDayClosingAndFinancialAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "financial_movement_audit_logs",
                schema: "operations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RouteId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Concept = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Details = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    PerformedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PerformedByDisplayName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    PerformedByUserRole = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TimestampUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_movement_audit_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "route_day_closings",
                schema: "operations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RouteId = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IsClosed = table.Column<bool>(type: "boolean", nullable: false),
                    ClosedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClosedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClosedByDisplayName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    ReopenedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReopenedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReopenedByDisplayName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_route_day_closings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_financial_movement_audit_logs_TenantId_EntityId",
                schema: "operations",
                table: "financial_movement_audit_logs",
                columns: new[] { "TenantId", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_financial_movement_audit_logs_TenantId_RouteId_TimestampUtc",
                schema: "operations",
                table: "financial_movement_audit_logs",
                columns: new[] { "TenantId", "RouteId", "TimestampUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_route_day_closings_TenantId_RouteId_BusinessDate",
                schema: "operations",
                table: "route_day_closings",
                columns: new[] { "TenantId", "RouteId", "BusinessDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "financial_movement_audit_logs",
                schema: "operations");

            migrationBuilder.DropTable(
                name: "route_day_closings",
                schema: "operations");
        }
    }
}
