using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GastroCore.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameBasePriceToIngredientsTotalPrice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "BasePrice",
                table: "Recipes",
                newName: "IngredientsTotalPrice");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IngredientsTotalPrice",
                table: "Recipes",
                newName: "BasePrice");
        }
    }
}
