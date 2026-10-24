using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MS.Cobroup.Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerIdentification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Identification",
                schema: "operations",
                table: "customers",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            // Only rows that exist at migration time receive the legacy marker.
            migrationBuilder.Sql("UPDATE operations.customers SET \"Identification\" = '0' WHERE \"Identification\" IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "Identification",
                schema: "operations",
                table: "customers",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Identification",
                schema: "operations",
                table: "customers");
        }
    }
}
