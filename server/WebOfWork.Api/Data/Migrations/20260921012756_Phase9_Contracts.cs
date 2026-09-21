using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace WebOfWork.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase9_Contracts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "contracts",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    project_id = table.Column<int>(type: "integer", nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    freelancer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    package_id = table.Column<Guid>(type: "uuid", nullable: true),
                    total_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    is_funded = table.Column<bool>(type: "boolean", nullable: false),
                    paid_to_freelancer = table.Column<bool>(type: "boolean", nullable: false),
                    payout_eligible_after = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_milestone_based = table.Column<bool>(type: "boolean", nullable: false),
                    milestone_count = table.Column<int>(type: "integer", nullable: false),
                    client_signature = table.Column<string>(type: "text", nullable: false),
                    client_signature_type = table.Column<string>(type: "text", nullable: false),
                    freelancer_signature = table.Column<string>(type: "text", nullable: true),
                    freelancer_signature_type = table.Column<string>(type: "text", nullable: true),
                    client_signed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    client_ip = table.Column<string>(type: "text", nullable: true),
                    freelancer_signed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    freelancer_ip = table.Column<string>(type: "text", nullable: true),
                    legal_document_url = table.Column<string>(type: "text", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_contracts", x => x.id);
                    table.ForeignKey(
                        name: "fk_contracts_client_profiles_client_id",
                        column: x => x.client_id,
                        principalTable: "client_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_contracts_freelancer_packages_package_id",
                        column: x => x.package_id,
                        principalTable: "freelancer_packages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_contracts_freelancer_profiles_freelancer_id",
                        column: x => x.freelancer_id,
                        principalTable: "freelancer_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_contracts_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_contracts_client_id",
                table: "contracts",
                column: "client_id");

            migrationBuilder.CreateIndex(
                name: "ix_contracts_client_signed_at",
                table: "contracts",
                column: "client_signed_at");

            migrationBuilder.CreateIndex(
                name: "ix_contracts_freelancer_id",
                table: "contracts",
                column: "freelancer_id");

            migrationBuilder.CreateIndex(
                name: "ix_contracts_package_id",
                table: "contracts",
                column: "package_id");

            migrationBuilder.CreateIndex(
                name: "ix_contracts_project_id",
                table: "contracts",
                column: "project_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_contracts_status",
                table: "contracts",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "contracts");
        }
    }
}
