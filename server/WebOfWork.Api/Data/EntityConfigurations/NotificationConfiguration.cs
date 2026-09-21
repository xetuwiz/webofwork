using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebOfWork.Api.Models;

namespace WebOfWork.Api.Data.EntityConfigurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.Property(n => n.CreatedAt)
               .HasColumnName("created_at");
        builder.Property(n => n.UpdatedAt)
               .HasColumnName("updated_at");
        builder.Property(n => n.RecipientId)
               .HasColumnName("recipient_id");
        builder.Property(n => n.RelatedEntityType)
               .HasColumnName("related_entity_type");
        builder.Property(n => n.RelatedEntityId)
               .HasColumnName("related_entity_id");

        builder.HasIndex(n => new { n.RecipientId, n.IsRead, n.CreatedAt })
               .HasDatabaseName("IX_notifications_recipient_read_created");

        builder.HasIndex(n => new { n.RelatedEntityType, n.RelatedEntityId })
               .HasFilter("related_entity_type IS NOT NULL AND related_entity_id IS NOT NULL");

        builder.Property(n => n.Type)
               .HasConversion<string>()
               .HasMaxLength(50);

        builder.Property(n => n.Message)
               .IsRequired()
               .HasMaxLength(1000);

        builder.Property(n => n.RelatedEntityType)
               .HasMaxLength(64);

        builder.HasOne(n => n.Recipient)
               .WithMany()
               .HasForeignKey(n => n.RecipientId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
