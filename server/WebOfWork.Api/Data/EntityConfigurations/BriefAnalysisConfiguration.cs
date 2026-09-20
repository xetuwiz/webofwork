using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace WebOfWork.Api.Data.EntityConfigurations;

public class BriefAnalysisConfiguration : IEntityTypeConfiguration<Models.BriefAnalysis>
{
    public void Configure(EntityTypeBuilder<Models.BriefAnalysis> builder)
    {
        builder.ToTable("brief_analyses");
        
        builder.Property(a => a.SuggestedBudget).HasColumnType("decimal(18,2)");
        builder.Property(a => a.Confidence).HasColumnType("decimal(5,4)");
        
        builder.HasOne(a => a.Brief)
               .WithOne(b => b.Analysis)
               .HasForeignKey<Models.BriefAnalysis>(a => a.BriefId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
