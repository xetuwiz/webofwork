using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebOfWork.Api.Models;

namespace WebOfWork.Api.Data.EntityConfigurations;

public class MilestoneConfiguration : IEntityTypeConfiguration<Milestone>
{
    public void Configure(EntityTypeBuilder<Milestone> builder)
    {
        builder.ToTable("milestones");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Amount).HasColumnType("decimal(18,2)");
        builder.Property(m => m.Title).IsRequired().HasMaxLength(255);
        
        builder.HasOne(m => m.Contract)
            .WithMany()
            .HasForeignKey(m => m.ContractId)
            .OnDelete(DeleteBehavior.Cascade);

        // Enforce uniqueness of milestone order per contract
        builder.HasIndex(m => new { m.ContractId, m.Order }).IsUnique();
        
        builder.HasIndex(m => m.Status);
    }
}
