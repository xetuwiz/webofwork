using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace WebOfWork.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase7_Briefs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "briefs",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "text", nullable: false),
                    budget_hint = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    deadline_hint = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_briefs", x => x.id);
                    table.ForeignKey(
                        name: "fk_briefs_client_profiles_client_id",
                        column: x => x.client_id,
                        principalTable: "client_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "brief_analyses",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    brief_id = table.Column<int>(type: "integer", nullable: false),
                    required_skills = table.Column<List<string>>(type: "text[]", nullable: false),
                    complexity = table.Column<string>(type: "text", nullable: false),
                    suggested_budget = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    timeline = table.Column<string>(type: "text", nullable: false),
                    confidence = table.Column<decimal>(type: "numeric(5,4)", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_brief_analyses", x => x.id);
                    table.ForeignKey(
                        name: "fk_brief_analyses_briefs_brief_id",
                        column: x => x.brief_id,
                        principalTable: "briefs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "brief_attachments",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    brief_id = table.Column<int>(type: "integer", nullable: false),
                    file_url = table.Column<string>(type: "text", nullable: false),
                    file_name = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_brief_attachments", x => x.id);
                    table.ForeignKey(
                        name: "fk_brief_attachments_briefs_brief_id",
                        column: x => x.brief_id,
                        principalTable: "briefs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_brief_analyses_brief_id",
                table: "brief_analyses",
                column: "brief_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_brief_attachments_brief_id",
                table: "brief_attachments",
                column: "brief_id");

            migrationBuilder.CreateIndex(
                name: "ix_briefs_client_id",
                table: "briefs",
                column: "client_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "brief_analyses");

            migrationBuilder.DropTable(
                name: "brief_attachments");

            migrationBuilder.DropTable(
                name: "briefs");
        }
    }
}
