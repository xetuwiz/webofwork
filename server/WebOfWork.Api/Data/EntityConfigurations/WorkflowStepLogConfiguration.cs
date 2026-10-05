using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebOfWork.Api.Models;

namespace WebOfWork.Api.Data.EntityConfigurations;

public class WorkflowStepLogConfiguration : IEntityTypeConfiguration<WorkflowStepLog>
{
    public void Configure(EntityTypeBuilder<WorkflowStepLog> builder)
    {
        builder.ToTable("workflow_step_logs");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.StepName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.AgentName).HasMaxLength(200);
        builder.Property(x => x.Input).HasColumnType("jsonb");
        builder.Property(x => x.Output).HasColumnType("jsonb");
        builder.Property(x => x.Status).IsRequired().HasMaxLength(50);

        builder.HasOne(x => x.WorkflowRun)
            .WithMany(r => r.StepLogs)
            .HasForeignKey(x => x.WorkflowRunId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.WorkflowRunId);
        builder.HasIndex(x => x.StepName);
        builder.HasIndex(x => x.CreatedAt);
    }
}
