using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebOfWork.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowApprovalRationale : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "workflow_run_id1",
                table: "workflow_step_logs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "approval_rationale",
                table: "workflow_runs",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_workflow_step_logs_workflow_run_id1",
                table: "workflow_step_logs",
                column: "workflow_run_id1");

            migrationBuilder.AddForeignKey(
                name: "fk_workflow_step_logs_workflow_runs_workflow_run_id1",
                table: "workflow_step_logs",
                column: "workflow_run_id1",
                principalTable: "workflow_runs",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_workflow_step_logs_workflow_runs_workflow_run_id1",
                table: "workflow_step_logs");

            migrationBuilder.DropIndex(
                name: "ix_workflow_step_logs_workflow_run_id1",
                table: "workflow_step_logs");

            migrationBuilder.DropColumn(
                name: "workflow_run_id1",
                table: "workflow_step_logs");

            migrationBuilder.DropColumn(
                name: "approval_rationale",
                table: "workflow_runs");
        }
    }
}
