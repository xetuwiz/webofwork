using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebOfWork.Api.Models;

namespace WebOfWork.Api.Data.EntityConfigurations;

public class DisputeConfiguration : IEntityTypeConfiguration<Dispute>
{
    public void Configure(EntityTypeBuilder<Dispute> builder)
    {
        builder.HasKey(d => d.Id);

        builder.HasOne(d => d.Contract)
            .WithMany()
            .HasForeignKey(d => d.ContractId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.Opener)
            .WithMany()
            .HasForeignKey(d => d.OpenerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(d => d.Description).IsRequired();
        builder.Property(d => d.Status).IsRequired();
        builder.Property(d => d.Reason).IsRequired();

        builder.HasMany(d => d.Evidence)
            .WithOne(f => f.Dispute)
            .HasForeignKey(f => f.DisputeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
