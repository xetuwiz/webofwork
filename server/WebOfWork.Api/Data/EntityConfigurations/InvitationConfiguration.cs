using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebOfWork.Api.Models;

namespace WebOfWork.Api.Data.EntityConfigurations;

public class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
{
    public void Configure(EntityTypeBuilder<Invitation> builder)
    {
        builder.ToTable("invitations");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Message).HasMaxLength(2000);
        builder.Property(i => i.Status).IsRequired();

        builder.HasOne(i => i.Client)
            .WithMany()
            .HasForeignKey(i => i.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.Freelancer)
            .WithMany()
            .HasForeignKey(i => i.FreelancerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(i => i.Project)
            .WithMany()
            .HasForeignKey(i => i.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(i => i.Package)
            .WithMany()
            .HasForeignKey(i => i.PackageId)
            .OnDelete(DeleteBehavior.Restrict);

        // Prevent multiple invitations from same client to same freelancer on same project
        builder.HasIndex(i => new { i.ProjectId, i.FreelancerId }).IsUnique();
        
        builder.HasIndex(i => i.Status);
        builder.HasIndex(i => i.CreatedAt);
    }
}
