using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MS.Cobroup.Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerAdditionalEvidences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "customer_additional_evidences",
                schema: "operations",
                columns: table => new
                {
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Slot = table.Column<int>(type: "integer", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ByteSize = table.Column<long>(type: "bigint", nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FileHashSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UploadedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_additional_evidences", x => new { x.CustomerId, x.Slot });
                    table.CheckConstraint("CK_CustomerEvidence_Slot", "\"Slot\" BETWEEN 2 AND 5");
                    table.ForeignKey(
                        name: "FK_customer_additional_evidences_customers_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "operations",
                        principalTable: "customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "customer_additional_evidences",
                schema: "operations");
        }
    }
}
