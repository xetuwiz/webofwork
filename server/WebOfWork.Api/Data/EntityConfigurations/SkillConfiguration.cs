using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebOfWork.Api.Models;

namespace WebOfWork.Api.Data.EntityConfigurations;

public class SkillConfiguration : IEntityTypeConfiguration<Skill>
{
    public void Configure(EntityTypeBuilder<Skill> builder)
    {
        builder.ToTable("skills");

        builder.HasKey(s => s.Id);
        
        builder.Property(s => s.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(s => new { s.Name, s.CategoryId })
            .IsUnique();

        builder.HasIndex(s => s.IsCustom);

        // Also restrict if FreelancerSkill is using it
        builder.HasMany<FreelancerSkill>()
            .WithOne()
            .HasForeignKey(fs => fs.SkillId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
