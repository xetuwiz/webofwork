using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebOfWork.Api.Models;

namespace WebOfWork.Api.Data.EntityConfigurations;

public class CmsSectionConfiguration : IEntityTypeConfiguration<CmsSection>
{
    public void Configure(EntityTypeBuilder<CmsSection> builder)
    {
        builder.Property(s => s.CreatedAt).HasColumnName("created_at");
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at");
        builder.Property(s => s.Category).HasColumnName("category").HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.Heading).HasColumnName("heading").IsRequired().HasMaxLength(255);
        builder.Property(s => s.Description).HasColumnName("description").IsRequired().HasMaxLength(8000);
        builder.Property(s => s.SortOrder).HasColumnName("sort_order");
        builder.Property(s => s.IsActive).HasColumnName("is_active");

        builder.HasIndex(s => new { s.Category, s.SortOrder })
               .HasDatabaseName("IX_cms_sections_category_sort_order")
               .HasFilter("is_active = true");
    }
}
