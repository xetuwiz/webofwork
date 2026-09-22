using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebOfWork.Api.Models;

namespace WebOfWork.Api.Data.EntityConfigurations;

public class FaqConfiguration : IEntityTypeConfiguration<Faq>
{
    public void Configure(EntityTypeBuilder<Faq> builder)
    {
        builder.Property(f => f.CreatedAt).HasColumnName("created_at");
        builder.Property(f => f.UpdatedAt).HasColumnName("updated_at");
        builder.Property(f => f.Question).HasColumnName("question").IsRequired().HasMaxLength(500);
        builder.Property(f => f.Answer).HasColumnName("answer").IsRequired().HasMaxLength(8000);
        builder.Property(f => f.Category).HasColumnName("category").HasConversion<string>().HasMaxLength(20);
        builder.Property(f => f.SortOrder).HasColumnName("sort_order");
        builder.Property(f => f.IsActive).HasColumnName("is_active");

        builder.HasIndex(f => new { f.Category, f.SortOrder })
               .HasDatabaseName("IX_faq_category_sort_order")
               .HasFilter("is_active = true");
    }
}
