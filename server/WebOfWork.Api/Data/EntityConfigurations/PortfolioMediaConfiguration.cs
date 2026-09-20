using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebOfWork.Api.Models;

namespace WebOfWork.Api.Data.EntityConfigurations;

public class PortfolioMediaConfiguration : IEntityTypeConfiguration<PortfolioMedia>
{
    public void Configure(EntityTypeBuilder<PortfolioMedia> builder)
    {
        builder.ToTable("portfolio_media");
        builder.HasKey(x => x.Id);
        
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.PortfolioItemId).HasColumnName("portfolio_item_id");
        builder.Property(x => x.MediaType).HasColumnName("media_type");
        builder.Property(x => x.Url).HasColumnName("url").IsRequired().HasMaxLength(1000);
        builder.Property(x => x.ThumbnailUrl).HasColumnName("thumbnail_url").HasMaxLength(1000);
        builder.Property(x => x.Caption).HasColumnName("caption").HasMaxLength(255);
        builder.Property(x => x.OrderIndex).HasColumnName("order_index").HasDefaultValue(0);
        builder.Property(x => x.PublicId).HasColumnName("public_id").HasMaxLength(255);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(x => x.PortfolioItem)
            .WithMany(p => p.Media)
            .HasForeignKey(x => x.PortfolioItemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
