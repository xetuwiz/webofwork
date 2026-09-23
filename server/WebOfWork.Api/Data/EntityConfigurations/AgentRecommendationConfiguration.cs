using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebOfWork.Api.Models;

namespace WebOfWork.Api.Data.EntityConfigurations;

public class AgentRecommendationConfiguration : IEntityTypeConfiguration<AgentRecommendation>
{
    public void Configure(EntityTypeBuilder<AgentRecommendation> builder)
    {
        builder.ToTable("agent_recommendations");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Score).HasColumnType("decimal(18,4)");
        builder.Property(x => x.FactorsJson).HasColumnType("jsonb");
        builder.Property(x => x.PortfolioSamplesJson).HasColumnType("jsonb");
        builder.Property(x => x.Reason).HasColumnType("text");

        builder.HasOne(x => x.WorkflowRun)
            .WithMany()
            .HasForeignKey(x => x.WorkflowRunId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.WorkflowRunId, x.ProjectId, x.FreelancerId });
        builder.HasIndex(x => x.CreatedAt);
    }
}
