using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SafeDeal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Fix_Disputes_OpenedByUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Disputes_Users_OpenedById",
                table: "Disputes");

            migrationBuilder.DropIndex(
                name: "IX_Disputes_OpenedById",
                table: "Disputes");

            migrationBuilder.DropColumn(
                name: "OpenedById",
                table: "Disputes");

            migrationBuilder.CreateIndex(
                name: "IX_Disputes_OpenedByUserId",
                table: "Disputes",
                column: "OpenedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Disputes_Users_OpenedByUserId",
                table: "Disputes",
                column: "OpenedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Disputes_Users_OpenedByUserId",
                table: "Disputes");

            migrationBuilder.DropIndex(
                name: "IX_Disputes_OpenedByUserId",
                table: "Disputes");

            migrationBuilder.AddColumn<int>(
                name: "OpenedById",
                table: "Disputes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Disputes_OpenedById",
                table: "Disputes",
                column: "OpenedById");

            migrationBuilder.AddForeignKey(
                name: "FK_Disputes_Users_OpenedById",
                table: "Disputes",
                column: "OpenedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
