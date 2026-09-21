using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebOfWork.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase8_Proposals_AllowResubmit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_proposals_project_id_freelancer_id",
                table: "proposals");

            migrationBuilder.CreateIndex(
                name: "ix_proposals_project_id_freelancer_id",
                table: "proposals",
                columns: new[] { "project_id", "freelancer_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_proposals_project_id_freelancer_id",
                table: "proposals");

            migrationBuilder.CreateIndex(
                name: "ix_proposals_project_id_freelancer_id",
                table: "proposals",
                columns: new[] { "project_id", "freelancer_id" },
                unique: true);
        }
    }
}
