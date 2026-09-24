using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebOfWork.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddExplicitDisputeOpeners : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_payments_contracts_contract_id1",
                table: "payments");

            migrationBuilder.DropIndex(
                name: "ix_payments_contract_id",
                table: "payments");

            migrationBuilder.DropIndex(
                name: "ix_payments_contract_id1",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "contract_id1",
                table: "payments");

            migrationBuilder.AddColumn<string>(
                name: "target_role",
                table: "notifications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "opened_by_client_id",
                table: "disputes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "opened_by_freelancer_id",
                table: "disputes",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_payments_contract_id",
                table: "payments",
                column: "contract_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_disputes_opened_by_client_id",
                table: "disputes",
                column: "opened_by_client_id");

            migrationBuilder.CreateIndex(
                name: "ix_disputes_opened_by_freelancer_id",
                table: "disputes",
                column: "opened_by_freelancer_id");

            migrationBuilder.AddForeignKey(
                name: "fk_disputes_client_profiles_opened_by_client_id",
                table: "disputes",
                column: "opened_by_client_id",
                principalTable: "client_profiles",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_disputes_freelancer_profiles_opened_by_freelancer_id",
                table: "disputes",
                column: "opened_by_freelancer_id",
                principalTable: "freelancer_profiles",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_disputes_client_profiles_opened_by_client_id",
                table: "disputes");

            migrationBuilder.DropForeignKey(
                name: "fk_disputes_freelancer_profiles_opened_by_freelancer_id",
                table: "disputes");

            migrationBuilder.DropIndex(
                name: "ix_payments_contract_id",
                table: "payments");

            migrationBuilder.DropIndex(
                name: "ix_disputes_opened_by_client_id",
                table: "disputes");

            migrationBuilder.DropIndex(
                name: "ix_disputes_opened_by_freelancer_id",
                table: "disputes");

            migrationBuilder.DropColumn(
                name: "target_role",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "opened_by_client_id",
                table: "disputes");

            migrationBuilder.DropColumn(
                name: "opened_by_freelancer_id",
                table: "disputes");

            migrationBuilder.AddColumn<int>(
                name: "contract_id1",
                table: "payments",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_payments_contract_id",
                table: "payments",
                column: "contract_id");

            migrationBuilder.CreateIndex(
                name: "ix_payments_contract_id1",
                table: "payments",
                column: "contract_id1",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_payments_contracts_contract_id1",
                table: "payments",
                column: "contract_id1",
                principalTable: "contracts",
                principalColumn: "id");
        }
    }
}
