using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebOfWork.Api.Models;

namespace WebOfWork.Api.Data.EntityConfigurations;

public class ContractRevisionConfiguration : IEntityTypeConfiguration<ContractRevision>
{
    public void Configure(EntityTypeBuilder<ContractRevision> builder)
    {
        builder.ToTable("contract_revisions");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Reason).IsRequired().HasMaxLength(1000);
        builder.Property(r => r.RejectionMessage).HasMaxLength(1000);

        builder.HasOne(r => r.Contract)
            .WithMany()
            .HasForeignKey(r => r.ContractId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
