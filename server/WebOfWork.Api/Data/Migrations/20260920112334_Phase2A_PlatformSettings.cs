using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebOfWork.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase2A_PlatformSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "global_settings",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    support_email = table.Column<string>(type: "text", nullable: false),
                    commission_percentage = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    pay_pal_email = table.Column<string>(type: "text", nullable: false),
                    min_brief_confidence = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    proposal_screening_threshold = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    match_score_threshold = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    default_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_global_settings", x => x.id);
                });

            migrationBuilder.InsertData(
                table: "global_settings",
                columns: new[] { "id", "commission_percentage", "created_at", "default_currency", "match_score_threshold", "min_brief_confidence", "pay_pal_email", "proposal_screening_threshold", "support_email", "updated_at" },
                values: new object[] { 1, 10.00m, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "LKR", 0.60m, 0.60m, "payments@webofwork.lk", 0.50m, "support@webofwork.lk", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "global_settings");
        }
    }
}
