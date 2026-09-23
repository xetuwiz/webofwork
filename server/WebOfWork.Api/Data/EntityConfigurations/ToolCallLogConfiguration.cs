using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebOfWork.Api.Models;

namespace WebOfWork.Api.Data.EntityConfigurations;

public class ToolCallLogConfiguration : IEntityTypeConfiguration<ToolCallLog>
{
    public void Configure(EntityTypeBuilder<ToolCallLog> builder)
    {
        builder.ToTable("tool_call_logs");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ToolName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Input).HasColumnType("jsonb");
        builder.Property(x => x.Output).HasColumnType("jsonb");
        builder.Property(x => x.Success).IsRequired();

        builder.HasOne(x => x.WorkflowRun)
            .WithMany()
            .HasForeignKey(x => x.WorkflowRunId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.WorkflowRunId);
        builder.HasIndex(x => x.CreatedAt);
    }
}
