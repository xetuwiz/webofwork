using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebOfWork.Api.Models;

namespace WebOfWork.Api.Data.EntityConfigurations;

public class SkillEndorsementConfiguration : IEntityTypeConfiguration<SkillEndorsement>
{
    public void Configure(EntityTypeBuilder<SkillEndorsement> builder)
    {
        builder.ToTable("skill_endorsements");
        builder.HasKey(x => x.Id);
        
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.SkillId).HasColumnName("skill_id");
        builder.Property(x => x.EndorserId).HasColumnName("endorser_id");
        builder.Property(x => x.Comment).HasColumnName("comment").HasMaxLength(1000);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(x => x.Skill)
            .WithMany(s => s.Endorsements)
            .HasForeignKey(x => x.SkillId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Endorser)
            .WithMany()
            .HasForeignKey(x => x.EndorserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
