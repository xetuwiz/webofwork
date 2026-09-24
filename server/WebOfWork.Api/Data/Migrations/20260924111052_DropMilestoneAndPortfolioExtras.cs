using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace WebOfWork.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class DropMilestoneAndPortfolioExtras : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_payments_milestones_milestone_id",
                table: "payments");

            migrationBuilder.DropTable(
                name: "milestone_deliverables");

            migrationBuilder.DropTable(
                name: "portfolio_tags");

            migrationBuilder.DropTable(
                name: "skill_endorsements");

            migrationBuilder.DropTable(
                name: "milestones");

            migrationBuilder.DropIndex(
                name: "ix_payments_milestone_id",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "milestone_id",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "is_milestone_based",
                table: "contracts");

            migrationBuilder.DropColumn(
                name: "milestone_count",
                table: "contracts");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "milestone_id",
                table: "payments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_milestone_based",
                table: "contracts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "milestone_count",
                table: "contracts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "milestones",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    contract_id = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    expected_days = table.Column<int>(type: "integer", nullable: false),
                    funded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    order = table.Column<int>(type: "integer", nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_milestones", x => x.id);
                    table.ForeignKey(
                        name: "fk_milestones_contracts_contract_id",
                        column: x => x.contract_id,
                        principalTable: "contracts",
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
                    endorser_id = table.Column<Guid>(type: "uuid", nullable: false),
                    skill_id = table.Column<Guid>(type: "uuid", nullable: false),
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

            migrationBuilder.CreateTable(
                name: "milestone_deliverables",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    milestone_id = table.Column<int>(type: "integer", nullable: false),
                    file_url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    submitted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_milestone_deliverables", x => x.id);
                    table.ForeignKey(
                        name: "fk_milestone_deliverables_milestones_milestone_id",
                        column: x => x.milestone_id,
                        principalTable: "milestones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_payments_milestone_id",
                table: "payments",
                column: "milestone_id");

            migrationBuilder.CreateIndex(
                name: "ix_milestone_deliverables_milestone_id",
                table: "milestone_deliverables",
                column: "milestone_id");

            migrationBuilder.CreateIndex(
                name: "ix_milestones_contract_id_order",
                table: "milestones",
                columns: new[] { "contract_id", "order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_milestones_status",
                table: "milestones",
                column: "status");

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

            migrationBuilder.AddForeignKey(
                name: "fk_payments_milestones_milestone_id",
                table: "payments",
                column: "milestone_id",
                principalTable: "milestones",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
