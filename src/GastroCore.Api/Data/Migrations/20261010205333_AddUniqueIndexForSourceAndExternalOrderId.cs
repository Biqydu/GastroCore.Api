using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GastroCore.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueIndexForSourceAndExternalOrderId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Orders_Source_ExternalOrderId",
                table: "Orders",
                columns: new[] { "Source", "ExternalOrderId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_Source_ExternalOrderId",
                table: "Orders");
        }
    }
}
