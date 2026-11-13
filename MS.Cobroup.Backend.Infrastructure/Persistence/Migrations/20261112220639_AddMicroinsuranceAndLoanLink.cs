using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MS.Cobroup.Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMicroinsuranceAndLoanLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "LoanId",
                schema: "operations",
                table: "route_financial_entries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MicroinsuranceAmount",
                schema: "operations",
                table: "loans",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_route_financial_entries_LoanId",
                schema: "operations",
                table: "route_financial_entries",
                column: "LoanId");

            migrationBuilder.CreateIndex(
                name: "IX_route_financial_entries_TenantId_LoanId",
                schema: "operations",
                table: "route_financial_entries",
                columns: new[] { "TenantId", "LoanId" });

            migrationBuilder.AddForeignKey(
                name: "FK_route_financial_entries_loans_LoanId",
                schema: "operations",
                table: "route_financial_entries",
                column: "LoanId",
                principalSchema: "operations",
                principalTable: "loans",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_route_financial_entries_loans_LoanId",
                schema: "operations",
                table: "route_financial_entries");

            migrationBuilder.DropIndex(
                name: "IX_route_financial_entries_LoanId",
                schema: "operations",
                table: "route_financial_entries");

            migrationBuilder.DropIndex(
                name: "IX_route_financial_entries_TenantId_LoanId",
                schema: "operations",
                table: "route_financial_entries");

            migrationBuilder.DropColumn(
                name: "LoanId",
                schema: "operations",
                table: "route_financial_entries");

            migrationBuilder.DropColumn(
                name: "MicroinsuranceAmount",
                schema: "operations",
                table: "loans");
        }
    }
}
