using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace WebOfWork.Api.Data.EntityConfigurations;

public class BriefConfiguration : IEntityTypeConfiguration<Models.Brief>
{
    public void Configure(EntityTypeBuilder<Models.Brief> builder)
    {
        builder.ToTable("briefs");
        
        builder.Property(b => b.BudgetHint).HasColumnType("decimal(18,2)");
        
        builder.HasOne(b => b.Client)
               .WithMany(c => c.Briefs)
               .HasForeignKey(b => b.ClientId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
