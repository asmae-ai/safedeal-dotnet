using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SafeDeal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddListIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transactions_BuyerId",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_VendorId",
                table: "Transactions");

            migrationBuilder.CreateIndex(
                name: "IX_Users_CreatedAt",
                table: "Users",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Role",
                table: "Users",
                column: "Role");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_BuyerId_CreatedAt",
                table: "Transactions",
                columns: new[] { "BuyerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_CreatedAt",
                table: "Transactions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_Status",
                table: "Transactions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_VendorId_CreatedAt",
                table: "Transactions",
                columns: new[] { "VendorId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_CreatedAt",
                table: "Notifications",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_IdentityVerifications_Status_CreatedAt",
                table: "IdentityVerifications",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_IdentityVerifications_SumsubApplicantId",
                table: "IdentityVerifications",
                column: "SumsubApplicantId");

            migrationBuilder.CreateIndex(
                name: "IX_Disputes_CreatedAt",
                table: "Disputes",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Disputes_Status_CreatedAt",
                table: "Disputes",
                columns: new[] { "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_CreatedAt",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_Role",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_BuyerId_CreatedAt",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_CreatedAt",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_Status",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_VendorId_CreatedAt",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_UserId_CreatedAt",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_IdentityVerifications_Status_CreatedAt",
                table: "IdentityVerifications");

            migrationBuilder.DropIndex(
                name: "IX_IdentityVerifications_SumsubApplicantId",
                table: "IdentityVerifications");

            migrationBuilder.DropIndex(
                name: "IX_Disputes_CreatedAt",
                table: "Disputes");

            migrationBuilder.DropIndex(
                name: "IX_Disputes_Status_CreatedAt",
                table: "Disputes");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_BuyerId",
                table: "Transactions",
                column: "BuyerId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_VendorId",
                table: "Transactions",
                column: "VendorId");
        }
    }
}
