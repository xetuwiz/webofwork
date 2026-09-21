using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebOfWork.Api.Models;

namespace WebOfWork.Api.Data.EntityConfigurations;

public class ChatRoomConfiguration : IEntityTypeConfiguration<ChatRoom>
{
    public void Configure(EntityTypeBuilder<ChatRoom> builder)
    {
        builder.Property(cr => cr.IsDeleted)
               .HasColumnName("is_deleted");
        builder.Property(cr => cr.DeletedAt)
               .HasColumnName("deleted_at");
        builder.Property(cr => cr.CreatedAt)
               .HasColumnName("created_at");
        builder.Property(cr => cr.UpdatedAt)
               .HasColumnName("updated_at");
        builder.Property(cr => cr.LastMessageAt)
               .HasColumnName("last_message_at");
        builder.Property(cr => cr.ClientArchived)
               .HasColumnName("client_archived");
        builder.Property(cr => cr.FreelancerArchived)
               .HasColumnName("freelancer_archived");

        builder.HasIndex(cr => new { cr.ContractId, cr.ClientId, cr.FreelancerId })
               .HasDatabaseName("IX_chat_rooms_contract_participants_unique")
               .IsUnique()
               .HasFilter("is_deleted = false");

        builder.HasIndex(cr => cr.ClientId);
        builder.HasIndex(cr => cr.FreelancerId);
        builder.HasIndex(cr => cr.LastMessageAt);

        builder.HasOne(cr => cr.Contract)
               .WithMany()
               .HasForeignKey(cr => cr.ContractId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(cr => cr.Client)
               .WithMany()
               .HasForeignKey(cr => cr.ClientId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(cr => cr.Freelancer)
               .WithMany()
               .HasForeignKey(cr => cr.FreelancerId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}

public class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.Property(m => m.IsDeleted)
               .HasColumnName("is_deleted");
        builder.Property(m => m.DeletedAt)
               .HasColumnName("deleted_at");
        builder.Property(m => m.CreatedAt)
               .HasColumnName("created_at");
        builder.Property(m => m.UpdatedAt)
               .HasColumnName("updated_at");
        builder.Property(m => m.DeletedByUserId)
               .HasColumnName("deleted_by_user_id");

        builder.HasIndex(m => new { m.RoomId, m.Id });
        builder.HasIndex(m => m.SenderId);
        builder.HasIndex(m => m.IsRead);
        builder.HasIndex(m => m.CreatedAt);

        builder.Property(m => m.Text)
               .IsRequired()
               .HasMaxLength(4000);

        builder.HasOne(m => m.Room)
               .WithMany(r => r.Messages)
               .HasForeignKey(m => m.RoomId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.Sender)
               .WithMany()
               .HasForeignKey(m => m.SenderId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
