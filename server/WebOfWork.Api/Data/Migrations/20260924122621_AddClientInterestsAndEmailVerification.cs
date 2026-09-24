using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebOfWork.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClientInterestsAndEmailVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "email_otp",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pending_email",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "client_profile_categories",
                columns: table => new
                {
                    client_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_client_profile_categories", x => new { x.client_profile_id, x.category_id });
                    table.ForeignKey(
                        name: "fk_client_profile_categories_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_client_profile_categories_client_profiles_client_profile_id",
                        column: x => x.client_profile_id,
                        principalTable: "client_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_client_profile_categories_category_id",
                table: "client_profile_categories",
                column: "category_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "client_profile_categories");

            migrationBuilder.DropColumn(
                name: "email_otp",
                table: "users");

            migrationBuilder.DropColumn(
                name: "pending_email",
                table: "users");
        }
    }
}
