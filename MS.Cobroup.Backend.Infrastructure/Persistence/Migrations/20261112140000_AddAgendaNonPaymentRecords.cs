using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MS.Cobroup.Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAgendaNonPaymentRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agenda_non_payment_reasons",
                schema: "operations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agenda_non_payment_reasons", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "agenda_customer_non_payment_records",
                schema: "operations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RouteId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    LoanId = table.Column<Guid>(type: "uuid", nullable: true),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ReasonId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReasonName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Observations = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RecordedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agenda_customer_non_payment_records", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_agenda_non_payment_reasons_TenantId_IsActive_DisplayOrder",
                schema: "operations",
                table: "agenda_non_payment_reasons",
                columns: new[] { "TenantId", "IsActive", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_agenda_customer_non_payment_records_TenantId_CustomerId_Bus~",
                schema: "operations",
                table: "agenda_customer_non_payment_records",
                columns: new[] { "TenantId", "CustomerId", "BusinessDate" });

            migrationBuilder.CreateIndex(
                name: "IX_agenda_customer_non_payment_records_TenantId_RouteId_Busine~",
                schema: "operations",
                table: "agenda_customer_non_payment_records",
                columns: new[] { "TenantId", "RouteId", "BusinessDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agenda_customer_non_payment_records",
                schema: "operations");

            migrationBuilder.DropTable(
                name: "agenda_non_payment_reasons",
                schema: "operations");
        }
    }
}

