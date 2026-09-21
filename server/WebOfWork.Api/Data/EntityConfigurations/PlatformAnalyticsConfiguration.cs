using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebOfWork.Api.Models;
namespace WebOfWork.Api.Data.EntityConfigurations;
public class PlatformAnalyticsConfiguration : IEntityTypeConfiguration<PlatformAnalytics>
{
    public void Configure(EntityTypeBuilder<PlatformAnalytics> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.TotalVolumeUsd).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(p => p.TotalProfitUsd).HasColumnType("decimal(18,2)").IsRequired();
    }
}
