using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebOfWork.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase7A_Projects_v2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_project_skills_project_id_skill_id",
                table: "project_skills");

            // Also drop single-column FK index before altering nullability
            migrationBuilder.DropIndex(
                name: "ix_project_skills_skill_id",
                table: "project_skills");

            migrationBuilder.AddColumn<int>(
                name: "category_id",
                table: "projects",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "expected_deliverables",
                table: "projects",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "projects",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "requirements",
                table: "projects",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<int>(
                name: "skill_id",
                table: "project_skills",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<string>(
                name: "custom_name",
                table: "project_skills",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_projects_category_id",
                table: "projects",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_projects_is_active",
                table: "projects",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_project_skills_project_id_skill_id",
                table: "project_skills",
                columns: new[] { "project_id", "skill_id" });

            migrationBuilder.AddForeignKey(
                name: "fk_projects_categories_category_id",
                table: "projects",
                column: "category_id",
                principalTable: "categories",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.CreateIndex(
                name: "ix_project_skills_skill_id",
                table: "project_skills",
                column: "skill_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_projects_categories_category_id",
                table: "projects");

            migrationBuilder.DropIndex(
                name: "ix_projects_category_id",
                table: "projects");

            migrationBuilder.DropIndex(
                name: "ix_projects_is_active",
                table: "projects");

            migrationBuilder.DropIndex(
                name: "ix_project_skills_project_id_skill_id",
                table: "project_skills");

            migrationBuilder.DropColumn(
                name: "category_id",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "expected_deliverables",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "requirements",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "custom_name",
                table: "project_skills");

            migrationBuilder.DropIndex(
                name: "ix_project_skills_skill_id",
                table: "project_skills");

            migrationBuilder.AlterColumn<int>(
                name: "skill_id",
                table: "project_skills",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_project_skills_project_id_skill_id",
                table: "project_skills",
                columns: new[] { "project_id", "skill_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_project_skills_skill_id",
                table: "project_skills",
                column: "skill_id");
        }
    }
}
