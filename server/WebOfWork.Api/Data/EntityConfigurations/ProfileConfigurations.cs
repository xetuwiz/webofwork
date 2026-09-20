using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebOfWork.Api.Models;

namespace WebOfWork.Api.Data.EntityConfigurations;

public class ClientProfileConfiguration : IEntityTypeConfiguration<ClientProfile>
{
    public void Configure(EntityTypeBuilder<ClientProfile> builder)
    {
        builder.ToTable("client_profiles");
        builder.HasKey(c => c.Id);
        
        builder.HasOne(c => c.User)
               .WithOne()
               .HasForeignKey<ClientProfile>(c => c.UserId)
               .OnDelete(DeleteBehavior.Cascade);
               
        builder.Property(c => c.TotalSpent).HasColumnType("decimal(18,2)");
        builder.Property(c => c.AverageRating).HasColumnType("decimal(3,2)");
        
        builder.HasIndex(c => c.TotalSpent);
    }
}

public class FreelancerProfileConfiguration : IEntityTypeConfiguration<FreelancerProfile>
{
    public void Configure(EntityTypeBuilder<FreelancerProfile> builder)
    {
        builder.ToTable("freelancer_profiles");
        builder.HasKey(f => f.Id);
        
        builder.HasOne(f => f.User)
               .WithOne()
               .HasForeignKey<FreelancerProfile>(f => f.UserId)
               .OnDelete(DeleteBehavior.Cascade);
               
        builder.Property(f => f.TotalEarnings).HasColumnType("decimal(18,2)");
        builder.Property(f => f.AverageRating).HasColumnType("decimal(3,2)");
        
        builder.HasIndex(f => f.Availability);
        builder.HasIndex(f => f.ExperienceLevel);
        builder.HasIndex(f => f.TotalEarnings);
        builder.HasIndex(f => f.CreatedAt);
    }
}

public class FreelancerSkillConfiguration : IEntityTypeConfiguration<FreelancerSkill>
{
    public void Configure(EntityTypeBuilder<FreelancerSkill> builder)
    {
        builder.ToTable("freelancer_skills");
        builder.HasKey(s => s.Id);
        
        builder.HasOne(s => s.User)
               .WithMany()
               .HasForeignKey(s => s.UserId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}

public class FreelancerPackageConfiguration : IEntityTypeConfiguration<FreelancerPackage>
{
    public void Configure(EntityTypeBuilder<FreelancerPackage> builder)
    {
        builder.ToTable("freelancer_packages");
        builder.HasKey(p => p.Id);
        
        builder.HasOne(p => p.User)
               .WithMany()
               .HasForeignKey(p => p.UserId)
               .OnDelete(DeleteBehavior.Cascade);
               
        builder.Property(p => p.Price).HasColumnType("decimal(18,2)");
        
        builder.HasIndex(p => p.PackageType);
        builder.HasIndex(p => p.Price);
    }
}

public class FreelancerPortfolioConfiguration : IEntityTypeConfiguration<FreelancerPortfolio>
{
    public void Configure(EntityTypeBuilder<FreelancerPortfolio> builder)
    {
        builder.ToTable("freelancer_portfolios");
        builder.HasKey(p => p.Id);
        
        builder.HasOne(p => p.User)
               .WithMany()
               .HasForeignKey(p => p.UserId)
               .OnDelete(DeleteBehavior.Cascade);
               
        builder.HasIndex(p => p.CreatedAt);
    }
}

public class FreelancerPaymentSettingsConfiguration : IEntityTypeConfiguration<FreelancerPaymentSettings>
{
    public void Configure(EntityTypeBuilder<FreelancerPaymentSettings> builder)
    {
        builder.ToTable("freelancer_payment_settings");
        builder.HasKey(p => p.Id);
        
        builder.HasOne(p => p.User)
               .WithOne()
               .HasForeignKey<FreelancerPaymentSettings>(p => p.UserId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}

public class FreelancerTodoConfiguration : IEntityTypeConfiguration<FreelancerTodo>
{
    public void Configure(EntityTypeBuilder<FreelancerTodo> builder)
    {
        builder.ToTable("freelancer_todos");
        builder.HasKey(t => t.Id);
        
        builder.HasOne(t => t.User)
               .WithMany()
               .HasForeignKey(t => t.UserId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
