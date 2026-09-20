using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebOfWork.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase5A_PortfolioMedia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "portfolio_media",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    portfolio_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    media_type = table.Column<int>(type: "integer", nullable: false),
                    url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    thumbnail_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    caption = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    order_index = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    public_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_portfolio_media", x => x.id);
                    table.ForeignKey(
                        name: "fk_portfolio_media_freelancer_portfolios_portfolio_item_id",
                        column: x => x.portfolio_item_id,
                        principalTable: "freelancer_portfolios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "portfolio_tags",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    portfolio_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    skill_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_portfolio_tags", x => x.id);
                    table.ForeignKey(
                        name: "fk_portfolio_tags_freelancer_portfolios_portfolio_item_id",
                        column: x => x.portfolio_item_id,
                        principalTable: "freelancer_portfolios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_portfolio_tags_freelancer_skills_skill_id",
                        column: x => x.skill_id,
                        principalTable: "freelancer_skills",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "skill_endorsements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    skill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    endorser_id = table.Column<Guid>(type: "uuid", nullable: false),
                    comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_skill_endorsements", x => x.id);
                    table.ForeignKey(
                        name: "fk_skill_endorsements_freelancer_skills_skill_id",
                        column: x => x.skill_id,
                        principalTable: "freelancer_skills",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_skill_endorsements_users_endorser_id",
                        column: x => x.endorser_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_portfolio_media_portfolio_item_id",
                table: "portfolio_media",
                column: "portfolio_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_portfolio_tags_portfolio_item_id",
                table: "portfolio_tags",
                column: "portfolio_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_portfolio_tags_skill_id",
                table: "portfolio_tags",
                column: "skill_id");

            migrationBuilder.CreateIndex(
                name: "ix_skill_endorsements_endorser_id",
                table: "skill_endorsements",
                column: "endorser_id");

            migrationBuilder.CreateIndex(
                name: "ix_skill_endorsements_skill_id",
                table: "skill_endorsements",
                column: "skill_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "portfolio_media");

            migrationBuilder.DropTable(
                name: "portfolio_tags");

            migrationBuilder.DropTable(
                name: "skill_endorsements");
        }
    }
}
