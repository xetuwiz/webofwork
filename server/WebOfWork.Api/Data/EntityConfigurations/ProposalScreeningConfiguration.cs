using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebOfWork.Api.Models;

namespace WebOfWork.Api.Data.EntityConfigurations;

public class ProposalScreeningConfiguration : IEntityTypeConfiguration<ProposalScreening>
{
    public void Configure(EntityTypeBuilder<ProposalScreening> builder)
    {
        builder.ToTable("proposal_screenings");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ProposalId).IsRequired();
        builder.Property(x => x.FitScore).HasColumnType("decimal(18,4)");
        builder.Property(x => x.StrengthsJson).HasColumnType("jsonb");
        builder.Property(x => x.ConcernsJson).HasColumnType("jsonb");
        builder.Property(x => x.Recommendation).IsRequired().HasMaxLength(50);
        builder.Property(x => x.Rationale).HasColumnType("text");

        builder.HasOne(x => x.WorkflowRun)
            .WithMany()
            .HasForeignKey(x => x.WorkflowRunId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.WorkflowRunId, x.ProposalId });
        builder.HasIndex(x => x.CreatedAt);
    }
}
