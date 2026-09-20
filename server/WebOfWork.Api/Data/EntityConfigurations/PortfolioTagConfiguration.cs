using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebOfWork.Api.Models;

namespace WebOfWork.Api.Data.EntityConfigurations;

public class PortfolioTagConfiguration : IEntityTypeConfiguration<PortfolioTag>
{
    public void Configure(EntityTypeBuilder<PortfolioTag> builder)
    {
        builder.ToTable("portfolio_tags");
        builder.HasKey(x => x.Id);
        
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.PortfolioItemId).HasColumnName("portfolio_item_id");
        builder.Property(x => x.SkillId).HasColumnName("skill_id");

        builder.HasOne(x => x.PortfolioItem)
            .WithMany(p => p.Tags)
            .HasForeignKey(x => x.PortfolioItemId)
            .OnDelete(DeleteBehavior.Cascade);
            
        builder.HasOne(x => x.Skill)
            .WithMany(s => s.PortfolioTags)
            .HasForeignKey(x => x.SkillId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
