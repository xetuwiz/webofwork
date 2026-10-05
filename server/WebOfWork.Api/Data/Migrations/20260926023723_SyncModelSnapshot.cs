using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebOfWork.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class SyncModelSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
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
                name: "fk_reviews_contracts_contract_id1",
                table: "reviews");

            migrationBuilder.DropForeignKey(
                name: "fk_workflow_step_logs_workflow_runs_workflow_run_id1",
                table: "workflow_step_logs");

            migrationBuilder.DropIndex(
                name: "ix_workflow_step_logs_workflow_run_id1",
                table: "workflow_step_logs");

            migrationBuilder.DropIndex(
                name: "ix_reviews_contract_id1",
                table: "reviews");

            migrationBuilder.DropIndex(
                name: "ix_disputes_contract_id",
                table: "disputes");

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
                name: "workflow_run_id1",
                table: "workflow_step_logs");

            migrationBuilder.DropColumn(
                name: "contract_id1",
                table: "reviews");

            migrationBuilder.DropColumn(
                name: "contract_id1",
                table: "disputes");

            migrationBuilder.DropColumn(
                name: "contract_id1",
                table: "contract_revisions");

            migrationBuilder.DropColumn(
                name: "contract_id1",
                table: "contract_deliverables");

            migrationBuilder.CreateIndex(
                name: "ix_disputes_contract_id",
                table: "disputes",
                column: "contract_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_disputes_contract_id",
                table: "disputes");

            migrationBuilder.AddColumn<int>(
                name: "workflow_run_id1",
                table: "workflow_step_logs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "contract_id1",
                table: "reviews",
                type: "integer",
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

            migrationBuilder.CreateIndex(
                name: "ix_workflow_step_logs_workflow_run_id1",
                table: "workflow_step_logs",
                column: "workflow_run_id1");

            migrationBuilder.CreateIndex(
                name: "ix_reviews_contract_id1",
                table: "reviews",
                column: "contract_id1");

            migrationBuilder.CreateIndex(
                name: "ix_disputes_contract_id",
                table: "disputes",
                column: "contract_id");

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
                name: "fk_reviews_contracts_contract_id1",
                table: "reviews",
                column: "contract_id1",
                principalTable: "contracts",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_workflow_step_logs_workflow_runs_workflow_run_id1",
                table: "workflow_step_logs",
                column: "workflow_run_id1",
                principalTable: "workflow_runs",
                principalColumn: "id");
        }
    }
}
