using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebOfWork.Api.Models;

namespace WebOfWork.Api.Data.EntityConfigurations;

public class ContractDeliverableConfiguration : IEntityTypeConfiguration<ContractDeliverable>
{
    public void Configure(EntityTypeBuilder<ContractDeliverable> builder)
    {
        builder.ToTable("contract_deliverables");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Title).IsRequired().HasMaxLength(255);
        builder.Property(d => d.FileUrl).HasMaxLength(1000);
        builder.Property(d => d.LinkUrl).HasMaxLength(1000);
        builder.Property(d => d.Notes).HasMaxLength(2000);

        builder.HasOne(d => d.Contract)
            .WithMany(c => c.Deliverables)
            .HasForeignKey(d => d.ContractId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(d => d.Freelancer)
            .WithMany()
            .HasForeignKey(d => d.FreelancerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
