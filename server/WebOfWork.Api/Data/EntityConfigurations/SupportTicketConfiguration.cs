using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebOfWork.Api.Models;

namespace WebOfWork.Api.Data.EntityConfigurations;

public class SupportTicketConfiguration : IEntityTypeConfiguration<SupportTicket>
{
    public void Configure(EntityTypeBuilder<SupportTicket> builder)
    {
        builder.Property(t => t.CreatedAt).HasColumnName("created_at");
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at");
        builder.Property(t => t.UserId).HasColumnName("user_id");
        builder.Property(t => t.SenderRole).HasColumnName("sender_role").HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.Subject).HasColumnName("subject").IsRequired().HasMaxLength(255);
        builder.Property(t => t.Message).HasColumnName("message").IsRequired().HasMaxLength(4000);
        builder.Property(t => t.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.AdminReply).HasColumnName("admin_reply").HasMaxLength(4000);
        builder.Property(t => t.ResolvedAt).HasColumnName("resolved_at");
        builder.Property(t => t.ResolvedByAdminId).HasColumnName("resolved_by_admin_id");

        builder.HasOne(t => t.User)
               .WithMany()
               .HasForeignKey(t => t.UserId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(t => t.ResolvedByAdmin)
               .WithMany()
               .HasForeignKey(t => t.ResolvedByAdminId)
               .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(t => new { t.UserId, t.Status })
               .HasDatabaseName("IX_support_tickets_user_status_open")
               .HasFilter("status = 'Open'");

        builder.HasIndex(t => new { t.Status, t.CreatedAt })
               .HasDatabaseName("IX_support_tickets_status_created_at");

        builder.HasIndex(t => t.SenderRole)
               .HasDatabaseName("IX_support_tickets_sender_role");
    }
}
