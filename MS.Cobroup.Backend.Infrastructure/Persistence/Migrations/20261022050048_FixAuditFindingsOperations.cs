using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MS.Cobroup.Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixAuditFindingsOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FileHashSha256",
                schema: "operations",
                table: "payment_evidences",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ConsumedAtUtc",
                schema: "operations",
                table: "loan_exception_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConsumedByLoanId",
                schema: "operations",
                table: "loan_exception_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsConsumed",
                schema: "operations",
                table: "loan_exception_requests",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_loans_TenantId_ExceptionRequestId",
                schema: "operations",
                table: "loans",
                columns: new[] { "TenantId", "ExceptionRequestId" },
                unique: true,
                filter: "\"ExceptionRequestId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_loan_exception_requests_TenantId_ConsumedByLoanId",
                schema: "operations",
                table: "loan_exception_requests",
                columns: new[] { "TenantId", "ConsumedByLoanId" },
                unique: true,
                filter: "\"ConsumedByLoanId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_loans_TenantId_ExceptionRequestId",
                schema: "operations",
                table: "loans");

            migrationBuilder.DropIndex(
                name: "IX_loan_exception_requests_TenantId_ConsumedByLoanId",
                schema: "operations",
                table: "loan_exception_requests");

            migrationBuilder.DropColumn(
                name: "FileHashSha256",
                schema: "operations",
                table: "payment_evidences");

            migrationBuilder.DropColumn(
                name: "ConsumedAtUtc",
                schema: "operations",
                table: "loan_exception_requests");

            migrationBuilder.DropColumn(
                name: "ConsumedByLoanId",
                schema: "operations",
                table: "loan_exception_requests");

            migrationBuilder.DropColumn(
                name: "IsConsumed",
                schema: "operations",
                table: "loan_exception_requests");
        }
    }
}
