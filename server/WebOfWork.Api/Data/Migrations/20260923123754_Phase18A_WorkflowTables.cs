using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace WebOfWork.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase18A_WorkflowTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "workflow_runs",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    objective = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    plan = table.Column<string>(type: "jsonb", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    project_id = table.Column<int>(type: "integer", nullable: true),
                    requested_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    approval_decision = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    approved_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    final_output = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workflow_runs", x => x.id);
                    table.ForeignKey(
                        name: "fk_workflow_runs_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_workflow_runs_users_approved_by_user_id",
                        column: x => x.approved_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_workflow_runs_users_requested_by_user_id",
                        column: x => x.requested_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "agent_recommendations",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    workflow_run_id = table.Column<int>(type: "integer", nullable: false),
                    project_id = table.Column<int>(type: "integer", nullable: false),
                    freelancer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    score = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    factors_json = table.Column<string>(type: "jsonb", nullable: true),
                    reason = table.Column<string>(type: "text", nullable: true),
                    portfolio_samples_json = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_agent_recommendations", x => x.id);
                    table.ForeignKey(
                        name: "fk_agent_recommendations_workflow_runs_workflow_run_id",
                        column: x => x.workflow_run_id,
                        principalTable: "workflow_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "proposal_screenings",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    workflow_run_id = table.Column<int>(type: "integer", nullable: false),
                    proposal_id = table.Column<int>(type: "integer", nullable: false),
                    fit_score = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    strengths_json = table.Column<string>(type: "jsonb", nullable: true),
                    concerns_json = table.Column<string>(type: "jsonb", nullable: true),
                    recommendation = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    rationale = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_proposal_screenings", x => x.id);
                    table.ForeignKey(
                        name: "fk_proposal_screenings_workflow_runs_workflow_run_id",
                        column: x => x.workflow_run_id,
                        principalTable: "workflow_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tool_call_logs",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    workflow_run_id = table.Column<int>(type: "integer", nullable: false),
                    tool_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    input = table.Column<string>(type: "jsonb", nullable: true),
                    output = table.Column<string>(type: "jsonb", nullable: true),
                    success = table.Column<bool>(type: "boolean", nullable: false),
                    duration_ms = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tool_call_logs", x => x.id);
                    table.ForeignKey(
                        name: "fk_tool_call_logs_workflow_runs_workflow_run_id",
                        column: x => x.workflow_run_id,
                        principalTable: "workflow_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "validation_results",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    workflow_run_id = table.Column<int>(type: "integer", nullable: false),
                    project_id = table.Column<int>(type: "integer", nullable: false),
                    proposal_id = table.Column<int>(type: "integer", nullable: false),
                    freelancer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    valid = table.Column<bool>(type: "boolean", nullable: false),
                    issues_json = table.Column<string>(type: "jsonb", nullable: true),
                    cost_breakdown_json = table.Column<string>(type: "jsonb", nullable: true),
                    requires_human_approval = table.Column<bool>(type: "boolean", nullable: false),
                    human_summary = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_validation_results", x => x.id);
                    table.ForeignKey(
                        name: "fk_validation_results_workflow_runs_workflow_run_id",
                        column: x => x.workflow_run_id,
                        principalTable: "workflow_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workflow_step_logs",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    workflow_run_id = table.Column<int>(type: "integer", nullable: false),
                    step_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    agent_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    input = table.Column<string>(type: "jsonb", nullable: true),
                    output = table.Column<string>(type: "jsonb", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    duration_ms = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workflow_step_logs", x => x.id);
                    table.ForeignKey(
                        name: "fk_workflow_step_logs_workflow_runs_workflow_run_id",
                        column: x => x.workflow_run_id,
                        principalTable: "workflow_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_agent_recommendations_created_at",
                table: "agent_recommendations",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_agent_recommendations_workflow_run_id_project_id_freelancer",
                table: "agent_recommendations",
                columns: new[] { "workflow_run_id", "project_id", "freelancer_id" });

            migrationBuilder.CreateIndex(
                name: "ix_proposal_screenings_created_at",
                table: "proposal_screenings",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_proposal_screenings_workflow_run_id_proposal_id",
                table: "proposal_screenings",
                columns: new[] { "workflow_run_id", "proposal_id" });

            migrationBuilder.CreateIndex(
                name: "ix_tool_call_logs_created_at",
                table: "tool_call_logs",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_tool_call_logs_workflow_run_id",
                table: "tool_call_logs",
                column: "workflow_run_id");

            migrationBuilder.CreateIndex(
                name: "ix_validation_results_created_at",
                table: "validation_results",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_validation_results_workflow_run_id_project_id_proposal_id_f",
                table: "validation_results",
                columns: new[] { "workflow_run_id", "project_id", "proposal_id", "freelancer_id" });

            migrationBuilder.CreateIndex(
                name: "ix_workflow_runs_approved_by_user_id",
                table: "workflow_runs",
                column: "approved_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_runs_created_at",
                table: "workflow_runs",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_runs_project_id",
                table: "workflow_runs",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_runs_requested_by_user_id",
                table: "workflow_runs",
                column: "requested_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_runs_status",
                table: "workflow_runs",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_step_logs_created_at",
                table: "workflow_step_logs",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_step_logs_step_name",
                table: "workflow_step_logs",
                column: "step_name");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_step_logs_workflow_run_id",
                table: "workflow_step_logs",
                column: "workflow_run_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agent_recommendations");

            migrationBuilder.DropTable(
                name: "proposal_screenings");

            migrationBuilder.DropTable(
                name: "tool_call_logs");

            migrationBuilder.DropTable(
                name: "validation_results");

            migrationBuilder.DropTable(
                name: "workflow_step_logs");

            migrationBuilder.DropTable(
                name: "workflow_runs");
        }
    }
}
