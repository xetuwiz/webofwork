using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebOfWork.Api.Models;

namespace WebOfWork.Api.Data.EntityConfigurations;

public class ProposalConfiguration : IEntityTypeConfiguration<Proposal>
{
    public void Configure(EntityTypeBuilder<Proposal> builder)
    {
        builder.ToTable("proposals");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.CoverLetter).IsRequired();
        builder.Property(p => p.BidAmount).HasColumnType("decimal(18,2)");
        builder.Property(p => p.DeliveryDays).IsRequired();
        builder.Property(p => p.Status).IsRequired();

        builder.HasOne(p => p.Project)
            .WithMany()
            .HasForeignKey(p => p.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.Freelancer)
            .WithMany()
            .HasForeignKey(p => p.FreelancerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.Package)
            .WithMany()
            .HasForeignKey(p => p.PackageId)
            .OnDelete(DeleteBehavior.Restrict);

        // Non-unique index — uniqueness enforced at service layer (Pending/Accepted only),
        // so a freelancer can re-propose after withdrawing.
        builder.HasIndex(p => new { p.ProjectId, p.FreelancerId });
        builder.HasIndex(p => p.Status);
        builder.HasIndex(p => p.CreatedAt);
    }
}
