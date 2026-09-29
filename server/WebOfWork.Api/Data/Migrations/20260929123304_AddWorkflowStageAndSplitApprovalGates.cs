using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebOfWork.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowStageAndSplitApprovalGates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "resume_value",
                table: "workflow_runs",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "stage",
                table: "workflow_runs",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "draft");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_runs_project_id_stage",
                table: "workflow_runs",
                columns: new[] { "project_id", "stage" });

            // The stage vocabulary is enforced here rather than by
            // `HasCheckConstraint` on the entity.
            //
            // EF's migration pipeline emits that correctly, but Npgsql's
            // `EnsureCreatedAsync` -- which every Testcontainers-backed test in this
            // project uses to build its schema -- orders the inline constraint
            // before the `stage` column exists and fails with
            // `42703: column "stage" does not exist`. Confirmed by removing the
            // constraint and watching the failure disappear.
            //
            // Declaring it on the entity would give a constraint that only some
            // schema-creation paths apply, which reads as a guarantee and is not
            // one. Here it is applied by migrations, so the real database enforces
            // it and a test database built by `EnsureCreated` honestly does not.
            //
            // The list is spelled out rather than interpolated from
            // `WorkflowStages.All` on purpose: a migration must not change meaning
            // when the code does. If a stage is renamed or removed later, this
            // constraint keeps describing the schema as it was at this point in
            // history.
            migrationBuilder.Sql("""
                ALTER TABLE workflow_runs
                ADD CONSTRAINT ck_workflow_runs_stage
                CHECK (stage IN ('draft', 'talent_match', 'screen', 'validate', 'outreach'));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_workflow_runs_project_id_stage",
                table: "workflow_runs");

            migrationBuilder.DropColumn(
                name: "resume_value",
                table: "workflow_runs");

            // Dropped before the columns it references, or Postgres refuses to drop
            // a column that a constraint depends on.
            migrationBuilder.Sql("""
                ALTER TABLE workflow_runs
                DROP CONSTRAINT IF EXISTS ck_workflow_runs_stage;
                """);

            migrationBuilder.DropColumn(
                name: "stage",
                table: "workflow_runs");
        }
    }
}
