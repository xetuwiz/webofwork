using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebOfWork.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase5_Profiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "client_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_name = table.Column<string>(type: "text", nullable: false),
                    location = table.Column<string>(type: "text", nullable: false),
                    average_rating = table.Column<decimal>(type: "numeric(3,2)", nullable: false),
                    review_count = table.Column<int>(type: "integer", nullable: false),
                    total_spent = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    total_projects_posted = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_client_profiles", x => x.id);
                    table.ForeignKey(
                        name: "fk_client_profiles_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "freelancer_packages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    package_type = table.Column<string>(type: "text", nullable: false),
                    price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    delivery_days = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_freelancer_packages", x => x.id);
                    table.ForeignKey(
                        name: "fk_freelancer_packages_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "freelancer_payment_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    paypal_email = table.Column<string>(type: "text", nullable: false),
                    is_verified = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_freelancer_payment_settings", x => x.id);
                    table.ForeignKey(
                        name: "fk_freelancer_payment_settings_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "freelancer_portfolios",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    image_url = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_freelancer_portfolios", x => x.id);
                    table.ForeignKey(
                        name: "fk_freelancer_portfolios_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "freelancer_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tagline = table.Column<string>(type: "text", nullable: false),
                    bio = table.Column<string>(type: "text", nullable: false),
                    phone = table.Column<string>(type: "text", nullable: false),
                    is_phone_verified = table.Column<bool>(type: "boolean", nullable: false),
                    average_rating = table.Column<decimal>(type: "numeric(3,2)", nullable: false),
                    review_count = table.Column<int>(type: "integer", nullable: false),
                    category_id = table.Column<int>(type: "integer", nullable: true),
                    experience_level = table.Column<string>(type: "text", nullable: false),
                    availability = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    total_earnings = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_freelancer_profiles", x => x.id);
                    table.ForeignKey(
                        name: "fk_freelancer_profiles_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "freelancer_skills",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    skill_id = table.Column<int>(type: "integer", nullable: true),
                    custom_name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_freelancer_skills", x => x.id);
                    table.ForeignKey(
                        name: "fk_freelancer_skills_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "freelancer_todos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    todo = table.Column<string>(type: "text", nullable: false),
                    is_completed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_freelancer_todos", x => x.id);
                    table.ForeignKey(
                        name: "fk_freelancer_todos_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_client_profiles_total_spent",
                table: "client_profiles",
                column: "total_spent");

            migrationBuilder.CreateIndex(
                name: "ix_client_profiles_user_id",
                table: "client_profiles",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_freelancer_packages_package_type",
                table: "freelancer_packages",
                column: "package_type");

            migrationBuilder.CreateIndex(
                name: "ix_freelancer_packages_price",
                table: "freelancer_packages",
                column: "price");

            migrationBuilder.CreateIndex(
                name: "ix_freelancer_packages_user_id",
                table: "freelancer_packages",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_freelancer_payment_settings_user_id",
                table: "freelancer_payment_settings",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_freelancer_portfolios_created_at",
                table: "freelancer_portfolios",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_freelancer_portfolios_user_id",
                table: "freelancer_portfolios",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_freelancer_profiles_availability",
                table: "freelancer_profiles",
                column: "availability");

            migrationBuilder.CreateIndex(
                name: "ix_freelancer_profiles_created_at",
                table: "freelancer_profiles",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_freelancer_profiles_experience_level",
                table: "freelancer_profiles",
                column: "experience_level");

            migrationBuilder.CreateIndex(
                name: "ix_freelancer_profiles_total_earnings",
                table: "freelancer_profiles",
                column: "total_earnings");

            migrationBuilder.CreateIndex(
                name: "ix_freelancer_profiles_user_id",
                table: "freelancer_profiles",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_freelancer_skills_user_id",
                table: "freelancer_skills",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_freelancer_todos_user_id",
                table: "freelancer_todos",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "client_profiles");

            migrationBuilder.DropTable(
                name: "freelancer_packages");

            migrationBuilder.DropTable(
                name: "freelancer_payment_settings");

            migrationBuilder.DropTable(
                name: "freelancer_portfolios");

            migrationBuilder.DropTable(
                name: "freelancer_profiles");

            migrationBuilder.DropTable(
                name: "freelancer_skills");

            migrationBuilder.DropTable(
                name: "freelancer_todos");
        }
    }
}
