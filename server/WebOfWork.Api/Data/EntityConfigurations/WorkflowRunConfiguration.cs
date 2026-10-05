using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebOfWork.Api.Models;

namespace WebOfWork.Api.Data.EntityConfigurations;

public class WorkflowRunConfiguration : IEntityTypeConfiguration<WorkflowRun>
{
    public void Configure(EntityTypeBuilder<WorkflowRun> builder)
    {
        // NOTE: the `ck_workflow_runs_stage` check constraint is applied in the
        // migration, and is deliberately absent from this model.
        //
        // Declaring it here with `ToTable(t => t.HasCheckConstraint(...))` produces
        // correct DDL from `dotnet ef migrations script`, but Npgsql's
        // `EnsureCreatedAsync` -- which every Testcontainers-backed test in this
        // project uses to build its schema -- emits the inline constraint before
        // the `stage` column exists and fails with
        // `42703: column "stage" does not exist`. Confirmed by removing the
        // constraint and watching the failure disappear.
        //
        // A constraint that only some schema-creation paths honour is worse than
        // none, because it reads as a guarantee. It lives in
        // `AddWorkflowStageAndSplitApprovalGates`, so the real database -- which is
        // built by migrations -- enforces it. A test database built by
        // `EnsureCreated` does not, and does not claim to.
        builder.ToTable("workflow_runs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Objective).IsRequired().HasMaxLength(2000);
        builder.Property(x => x.Plan).HasColumnType("jsonb");
        builder.Property(x => x.Status).IsRequired();
        builder.Property(x => x.ApprovalDecision).HasMaxLength(50);
        builder.Property(x => x.FinalOutput).HasColumnType("text");

        // `stage` is a required, length-capped string rather than an int enum
        // because the Python service owns this vocabulary and sends it back
        // verbatim on every call. The database-level check that keeps it honest is
        // in the migration -- see the note at the top of this file.
        //
        // `resume_value` is jsonb because the shape is per-stage: a shortlist
        // approval stores freelancer ids, a contract approval stores a spec.
        builder.Property(x => x.Stage).IsRequired().HasMaxLength(40).HasDefaultValue(WorkflowStages.Draft);
        builder.Property(x => x.ResumeValue).HasColumnType("jsonb");

        builder.HasOne(x => x.Project)
            .WithMany()
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.RequestedByUser)
            .WithMany()
            .HasForeignKey(x => x.RequestedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ApprovedByUser)
            .WithMany()
            .HasForeignKey(x => x.ApprovedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.ProjectId);
        builder.HasIndex(x => x.RequestedByUserId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.CreatedAt);

        // The admin monitor lists by stage as often as by status, and the
        // by-project lookup filters on both.
        builder.HasIndex(x => new { x.ProjectId, x.Stage });
    }
}
