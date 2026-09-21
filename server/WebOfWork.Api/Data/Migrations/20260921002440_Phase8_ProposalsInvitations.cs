using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace WebOfWork.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase8_ProposalsInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_project_skills_project_id_skill_id",
                table: "project_skills");

            migrationBuilder.CreateTable(
                name: "invitations",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    freelancer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<int>(type: "integer", nullable: false),
                    package_id = table.Column<Guid>(type: "uuid", nullable: false),
                    message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_invitations", x => x.id);
                    table.ForeignKey(
                        name: "fk_invitations_client_profiles_client_id",
                        column: x => x.client_id,
                        principalTable: "client_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_invitations_freelancer_packages_package_id",
                        column: x => x.package_id,
                        principalTable: "freelancer_packages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_invitations_freelancer_profiles_freelancer_id",
                        column: x => x.freelancer_id,
                        principalTable: "freelancer_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_invitations_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "proposals",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    project_id = table.Column<int>(type: "integer", nullable: false),
                    freelancer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    package_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cover_letter = table.Column<string>(type: "text", nullable: false),
                    bid_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    delivery_days = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_proposals", x => x.id);
                    table.ForeignKey(
                        name: "fk_proposals_freelancer_packages_package_id",
                        column: x => x.package_id,
                        principalTable: "freelancer_packages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_proposals_freelancer_profiles_freelancer_id",
                        column: x => x.freelancer_id,
                        principalTable: "freelancer_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_proposals_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_project_skills_project_id_skill_id",
                table: "project_skills",
                columns: new[] { "project_id", "skill_id" });

            migrationBuilder.CreateIndex(
                name: "ix_invitations_client_id",
                table: "invitations",
                column: "client_id");

            migrationBuilder.CreateIndex(
                name: "ix_invitations_created_at",
                table: "invitations",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_invitations_freelancer_id",
                table: "invitations",
                column: "freelancer_id");

            migrationBuilder.CreateIndex(
                name: "ix_invitations_package_id",
                table: "invitations",
                column: "package_id");

            migrationBuilder.CreateIndex(
                name: "ix_invitations_project_id_freelancer_id",
                table: "invitations",
                columns: new[] { "project_id", "freelancer_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_invitations_status",
                table: "invitations",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_proposals_created_at",
                table: "proposals",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_proposals_freelancer_id",
                table: "proposals",
                column: "freelancer_id");

            migrationBuilder.CreateIndex(
                name: "ix_proposals_package_id",
                table: "proposals",
                column: "package_id");

            migrationBuilder.CreateIndex(
                name: "ix_proposals_project_id_freelancer_id",
                table: "proposals",
                columns: new[] { "project_id", "freelancer_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_proposals_status",
                table: "proposals",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "invitations");

            migrationBuilder.DropTable(
                name: "proposals");

            migrationBuilder.DropIndex(
                name: "ix_project_skills_project_id_skill_id",
                table: "project_skills");

            migrationBuilder.CreateIndex(
                name: "ix_project_skills_project_id_skill_id",
                table: "project_skills",
                columns: new[] { "project_id", "skill_id" },
                unique: true,
                filter: "\"skill_id\" IS NOT NULL");
        }
    }
}
