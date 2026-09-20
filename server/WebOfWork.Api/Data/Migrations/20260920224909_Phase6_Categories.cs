using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace WebOfWork.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase6_Categories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "categories",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "skills",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    category_id = table.Column<int>(type: "integer", nullable: true),
                    is_custom = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_skills", x => x.id);
                    table.ForeignKey(
                        name: "fk_skills_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_freelancer_skills_skill_id",
                table: "freelancer_skills",
                column: "skill_id");

            migrationBuilder.CreateIndex(
                name: "ix_freelancer_profiles_category_id",
                table: "freelancer_profiles",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_categories_name",
                table: "categories",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_skills_category_id",
                table: "skills",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_skills_is_custom",
                table: "skills",
                column: "is_custom");

            migrationBuilder.CreateIndex(
                name: "ix_skills_name_category_id",
                table: "skills",
                columns: new[] { "name", "category_id" },
                unique: true);

            migrationBuilder.Sql("UPDATE freelancer_profiles SET category_id = NULL;");

            migrationBuilder.AddForeignKey(
                name: "fk_freelancer_profiles_categories_category_id",
                table: "freelancer_profiles",
                column: "category_id",
                principalTable: "categories",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql("UPDATE freelancer_skills SET skill_id = NULL;");

            migrationBuilder.AddForeignKey(
                name: "fk_freelancer_skills_skills_skill_id",
                table: "freelancer_skills",
                column: "skill_id",
                principalTable: "skills",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_freelancer_profiles_categories_category_id",
                table: "freelancer_profiles");

            migrationBuilder.DropForeignKey(
                name: "fk_freelancer_skills_skills_skill_id",
                table: "freelancer_skills");

            migrationBuilder.DropTable(
                name: "skills");

            migrationBuilder.DropTable(
                name: "categories");

            migrationBuilder.DropIndex(
                name: "ix_freelancer_skills_skill_id",
                table: "freelancer_skills");

            migrationBuilder.DropIndex(
                name: "ix_freelancer_profiles_category_id",
                table: "freelancer_profiles");
        }
    }
}
