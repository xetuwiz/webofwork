using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebOfWork.Api.Models;
namespace WebOfWork.Api.Data.EntityConfigurations;
public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.HasKey(p => p.Id);
        builder.HasIndex(p => p.PayPalOrderId).IsUnique();
        builder.HasOne(p => p.Contract).WithMany().HasForeignKey(p => p.ContractId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.Milestone).WithMany().HasForeignKey(p => p.MilestoneId).OnDelete(DeleteBehavior.SetNull);
        builder.Property(p => p.Amount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(p => p.PlatformFee).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(p => p.PayPalOrderId).IsRequired().HasMaxLength(255);
    }
}
