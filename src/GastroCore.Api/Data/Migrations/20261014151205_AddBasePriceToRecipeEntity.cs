using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GastroCore.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBasePriceToRecipeEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BasePrice",
                table: "Recipes",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BasePrice",
                table: "Recipes");
        }
    }
}
