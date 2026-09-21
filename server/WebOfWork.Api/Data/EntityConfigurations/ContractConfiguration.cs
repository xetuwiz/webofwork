using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebOfWork.Api.Models;

namespace WebOfWork.Api.Data.EntityConfigurations;

public class ContractConfiguration : IEntityTypeConfiguration<Contract>
{
    public void Configure(EntityTypeBuilder<Contract> builder)
    {
        builder.ToTable("contracts");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.TotalAmount).HasColumnType("decimal(18,2)");
        builder.Property(c => c.Status).IsRequired();

        builder.HasOne(c => c.Project)
            .WithOne()
            .HasForeignKey<Contract>(c => c.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.Client)
            .WithMany()
            .HasForeignKey(c => c.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.Freelancer)
            .WithMany()
            .HasForeignKey(c => c.FreelancerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.Package)
            .WithMany()
            .HasForeignKey(c => c.PackageId)
            .OnDelete(DeleteBehavior.SetNull);

        // A project can only have one contract
        builder.HasIndex(c => c.ProjectId).IsUnique();
        
        builder.HasIndex(c => c.Status);
        builder.HasIndex(c => c.ClientSignedAt);
    }
}
