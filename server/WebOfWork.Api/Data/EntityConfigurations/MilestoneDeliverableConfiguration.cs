using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebOfWork.Api.Models;

namespace WebOfWork.Api.Data.EntityConfigurations;

public class MilestoneDeliverableConfiguration : IEntityTypeConfiguration<MilestoneDeliverable>
{
    public void Configure(EntityTypeBuilder<MilestoneDeliverable> builder)
    {
        builder.ToTable("milestone_deliverables");

        builder.HasKey(md => md.Id);

        builder.Property(md => md.Notes).HasMaxLength(1000);
        builder.Property(md => md.FileUrl).HasMaxLength(2000);
        
        builder.HasOne(md => md.Milestone)
            .WithMany(m => m.Deliverables)
            .HasForeignKey(md => md.MilestoneId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
