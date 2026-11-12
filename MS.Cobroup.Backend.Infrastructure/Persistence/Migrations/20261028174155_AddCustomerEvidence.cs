using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MS.Cobroup.Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "EvidenceByteSize",
                schema: "operations",
                table: "customers",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EvidenceContentType",
                schema: "operations",
                table: "customers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EvidenceFileHashSha256",
                schema: "operations",
                table: "customers",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EvidenceFileName",
                schema: "operations",
                table: "customers",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EvidenceStorageKey",
                schema: "operations",
                table: "customers",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EvidenceUploadedAtUtc",
                schema: "operations",
                table: "customers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EvidenceUploadedByUserId",
                schema: "operations",
                table: "customers",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EvidenceByteSize",
                schema: "operations",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "EvidenceContentType",
                schema: "operations",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "EvidenceFileHashSha256",
                schema: "operations",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "EvidenceFileName",
                schema: "operations",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "EvidenceStorageKey",
                schema: "operations",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "EvidenceUploadedAtUtc",
                schema: "operations",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "EvidenceUploadedByUserId",
                schema: "operations",
                table: "customers");
        }
    }
}
