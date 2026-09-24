using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebOfWork.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLegacyContractAndProfileFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "current_role",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "is_freelancer",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "profile_photo",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "contract_id1",
                table: "reviews",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "contract_id1",
                table: "payments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "freelancer_share",
                table: "payments",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "pay_pal_capture_id",
                table: "payments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "contract_id1",
                table: "disputes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "contract_id1",
                table: "contract_revisions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "contract_id1",
                table: "contract_deliverables",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deliverable_type",
                table: "contract_deliverables",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "ix_reviews_contract_id1",
                table: "reviews",
                column: "contract_id1");

            migrationBuilder.CreateIndex(
                name: "ix_payments_contract_id1",
                table: "payments",
                column: "contract_id1",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_disputes_contract_id1",
                table: "disputes",
                column: "contract_id1",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_contract_revisions_contract_id1",
                table: "contract_revisions",
                column: "contract_id1");

            migrationBuilder.CreateIndex(
                name: "ix_contract_deliverables_contract_id1",
                table: "contract_deliverables",
                column: "contract_id1");

            migrationBuilder.AddForeignKey(
                name: "fk_contract_deliverables_contracts_contract_id1",
                table: "contract_deliverables",
                column: "contract_id1",
                principalTable: "contracts",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_contract_revisions_contracts_contract_id1",
                table: "contract_revisions",
                column: "contract_id1",
                principalTable: "contracts",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_disputes_contracts_contract_id1",
                table: "disputes",
                column: "contract_id1",
                principalTable: "contracts",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_payments_contracts_contract_id1",
                table: "payments",
                column: "contract_id1",
                principalTable: "contracts",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_reviews_contracts_contract_id1",
                table: "reviews",
                column: "contract_id1",
                principalTable: "contracts",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_contract_deliverables_contracts_contract_id1",
                table: "contract_deliverables");

            migrationBuilder.DropForeignKey(
                name: "fk_contract_revisions_contracts_contract_id1",
                table: "contract_revisions");

            migrationBuilder.DropForeignKey(
                name: "fk_disputes_contracts_contract_id1",
                table: "disputes");

            migrationBuilder.DropForeignKey(
                name: "fk_payments_contracts_contract_id1",
                table: "payments");

            migrationBuilder.DropForeignKey(
                name: "fk_reviews_contracts_contract_id1",
                table: "reviews");

            migrationBuilder.DropIndex(
                name: "ix_reviews_contract_id1",
                table: "reviews");

            migrationBuilder.DropIndex(
                name: "ix_payments_contract_id1",
                table: "payments");

            migrationBuilder.DropIndex(
                name: "ix_disputes_contract_id1",
                table: "disputes");

            migrationBuilder.DropIndex(
                name: "ix_contract_revisions_contract_id1",
                table: "contract_revisions");

            migrationBuilder.DropIndex(
                name: "ix_contract_deliverables_contract_id1",
                table: "contract_deliverables");

            migrationBuilder.DropColumn(
                name: "current_role",
                table: "users");

            migrationBuilder.DropColumn(
                name: "is_freelancer",
                table: "users");

            migrationBuilder.DropColumn(
                name: "profile_photo",
                table: "users");

            migrationBuilder.DropColumn(
                name: "contract_id1",
                table: "reviews");

            migrationBuilder.DropColumn(
                name: "contract_id1",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "freelancer_share",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "pay_pal_capture_id",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "contract_id1",
                table: "disputes");

            migrationBuilder.DropColumn(
                name: "contract_id1",
                table: "contract_revisions");

            migrationBuilder.DropColumn(
                name: "contract_id1",
                table: "contract_deliverables");

            migrationBuilder.DropColumn(
                name: "deliverable_type",
                table: "contract_deliverables");
        }
    }
}
