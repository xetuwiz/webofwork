using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebOfWork.Api.Models;

namespace WebOfWork.Api.Data.EntityConfigurations;

public class ValidationResultConfiguration : IEntityTypeConfiguration<ValidationResult>
{
    public void Configure(EntityTypeBuilder<ValidationResult> builder)
    {
        builder.ToTable("validation_results");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ProjectId).IsRequired();
        builder.Property(x => x.ProposalId).IsRequired();
        builder.Property(x => x.FreelancerId).IsRequired();
        builder.Property(x => x.Valid).IsRequired();
        builder.Property(x => x.IssuesJson).HasColumnType("jsonb");
        builder.Property(x => x.CostBreakdownJson).HasColumnType("jsonb");
        builder.Property(x => x.HumanSummary).HasColumnType("text");

        builder.HasOne(x => x.WorkflowRun)
            .WithMany()
            .HasForeignKey(x => x.WorkflowRunId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.WorkflowRunId, x.ProjectId, x.ProposalId, x.FreelancerId });
        builder.HasIndex(x => x.CreatedAt);
    }
}
