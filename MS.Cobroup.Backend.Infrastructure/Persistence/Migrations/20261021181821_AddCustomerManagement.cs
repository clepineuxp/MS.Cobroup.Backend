using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MS.Cobroup.Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "operations");

            migrationBuilder.CreateTable(
                name: "customers",
                schema: "operations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RouteId = table.Column<Guid>(type: "uuid", nullable: false),
                    FirstNames = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    LastNames = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    BusinessOrWorkName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    Observations = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "customer_locations",
                schema: "operations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    Longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    Alias = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    Source = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_locations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_customer_locations_customers_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "operations",
                        principalTable: "customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "customer_phones",
                schema: "operations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<string>(type: "character varying(21)", maxLength: 21, nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_phones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_customer_phones_customers_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "operations",
                        principalTable: "customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_customer_locations_CustomerId",
                schema: "operations",
                table: "customer_locations",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_customer_phones_CustomerId_Number",
                schema: "operations",
                table: "customer_phones",
                columns: new[] { "CustomerId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_customers_TenantId_LastNames_FirstNames",
                schema: "operations",
                table: "customers",
                columns: new[] { "TenantId", "LastNames", "FirstNames" });

            migrationBuilder.CreateIndex(
                name: "IX_customers_TenantId_RouteId_Status",
                schema: "operations",
                table: "customers",
                columns: new[] { "TenantId", "RouteId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "customer_locations",
                schema: "operations");

            migrationBuilder.DropTable(
                name: "customer_phones",
                schema: "operations");

            migrationBuilder.DropTable(
                name: "customers",
                schema: "operations");
        }
    }
}
