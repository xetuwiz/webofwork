using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebOfWork.Api.Models;

namespace WebOfWork.Api.Data.EntityConfigurations;

public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("projects");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Title).IsRequired().HasMaxLength(255);
        builder.Property(p => p.Description).IsRequired();
        builder.Property(p => p.Requirements).IsRequired().HasDefaultValue(string.Empty);
        builder.Property(p => p.ExpectedDeliverables).IsRequired().HasDefaultValue(string.Empty);
        builder.Property(p => p.IsActive).IsRequired().HasDefaultValue(true);

        builder.Property(p => p.FixedPrice).HasColumnType("decimal(18,2)");
        builder.Property(p => p.MinPrice).HasColumnType("decimal(18,2)");
        builder.Property(p => p.MaxPrice).HasColumnType("decimal(18,2)");

        // FK to ClientProfile
        builder.HasOne(p => p.Client)
            .WithMany()
            .HasForeignKey(p => p.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        // Optional FK to Category (mirrors ref: category = ForeignKey(Category, on_delete=PROTECT))
        builder.HasOne(p => p.Category)
            .WithMany()
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasIndex(p => p.Status);
        builder.HasIndex(p => p.CreatedAt);
        builder.HasIndex(p => p.IsActive);
    }
}

public class ProjectSkillConfiguration : IEntityTypeConfiguration<ProjectSkill>
{
    public void Configure(EntityTypeBuilder<ProjectSkill> builder)
    {
        builder.ToTable("project_skills");

        builder.HasKey(ps => ps.Id);

        builder.Property(ps => ps.CustomName).HasMaxLength(100);

        builder.HasOne(ps => ps.Project)
            .WithMany(p => p.Skills)
            .HasForeignKey(ps => ps.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        // SkillId is now nullable (mirrors ref: null=True, blank=True on skill FK)
        builder.HasOne(ps => ps.Skill)
            .WithMany()
            .HasForeignKey(ps => ps.SkillId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        // Reference model does not enforce unique project skills at the DB level
        builder.HasIndex(ps => new { ps.ProjectId, ps.SkillId });
    }
}
