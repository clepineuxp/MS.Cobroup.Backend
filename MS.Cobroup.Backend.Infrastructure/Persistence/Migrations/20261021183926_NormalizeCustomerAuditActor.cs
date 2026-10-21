using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MS.Cobroup.Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeCustomerAuditActor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE operations.customers
                SET "UpdatedByUserId" = "CreatedByUserId"
                WHERE "UpdatedByUserId" = '00000000-0000-0000-0000-000000000000';

                ALTER TABLE operations.customers
                ALTER COLUMN "UpdatedByUserId" DROP DEFAULT;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE operations.customers
                ALTER COLUMN "UpdatedByUserId" SET DEFAULT '00000000-0000-0000-0000-000000000000';
                """);
        }
    }
}
