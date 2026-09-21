using System.Reflection;
using Microsoft.EntityFrameworkCore;
using WebOfWork.Api.Common.Interfaces;

namespace WebOfWork.Api.Data;

public class WebOfWorkDbContext : DbContext
{
    public WebOfWorkDbContext(DbContextOptions<WebOfWorkDbContext> options) : base(options)
    {
    }

    // Phase 3
    public DbSet<WebOfWork.Api.Models.User> Users { get; set; }
    
    // Phase 5: Profiles
    public DbSet<WebOfWork.Api.Models.ClientProfile> ClientProfiles { get; set; }
    public DbSet<WebOfWork.Api.Models.FreelancerProfile> FreelancerProfiles { get; set; }
    public DbSet<WebOfWork.Api.Models.Category> Categories { get; set; }
    public DbSet<WebOfWork.Api.Models.Skill> Skills { get; set; }
    public DbSet<WebOfWork.Api.Models.Brief> Briefs { get; set; }
    public DbSet<WebOfWork.Api.Models.BriefAttachment> BriefAttachments { get; set; }
    public DbSet<WebOfWork.Api.Models.BriefAnalysis> BriefAnalyses { get; set; }
    public DbSet<WebOfWork.Api.Models.Project> Projects { get; set; }
    public DbSet<WebOfWork.Api.Models.ProjectSkill> ProjectSkills { get; set; }
    public DbSet<WebOfWork.Api.Models.Proposal> Proposals { get; set; }
    public DbSet<WebOfWork.Api.Models.Invitation> Invitations { get; set; }
    public DbSet<WebOfWork.Api.Models.Contract> Contracts { get; set; }
    public DbSet<WebOfWork.Api.Models.ContractDeliverable> ContractDeliverables { get; set; }
    public DbSet<WebOfWork.Api.Models.ContractRevision> ContractRevisions { get; set; }
    public DbSet<WebOfWork.Api.Models.Milestone> Milestones { get; set; }
    public DbSet<WebOfWork.Api.Models.MilestoneDeliverable> MilestoneDeliverables { get; set; }
    public DbSet<WebOfWork.Api.Models.Dispute> Disputes { get; set; }
    public DbSet<WebOfWork.Api.Models.DisputeFile> DisputeFiles { get; set; }
    public DbSet<WebOfWork.Api.Models.Review> Reviews { get; set; }
    public DbSet<WebOfWork.Api.Models.FreelancerSkill> FreelancerSkills { get; set; }
    public DbSet<WebOfWork.Api.Models.FreelancerPackage> FreelancerPackages { get; set; }
    public DbSet<WebOfWork.Api.Models.FreelancerPortfolio> FreelancerPortfolios { get; set; }
    public DbSet<WebOfWork.Api.Models.FreelancerPaymentSettings> FreelancerPaymentSettings { get; set; }
    public DbSet<WebOfWork.Api.Models.FreelancerTodo> FreelancerTodos { get; set; }
    
    // Phase 2A
    public DbSet<WebOfWork.Api.Models.PortfolioMedia> PortfolioMedia { get; set; }
    public DbSet<WebOfWork.Api.Models.PortfolioTag> PortfolioTags { get; set; }
    public DbSet<WebOfWork.Api.Models.SkillEndorsement> SkillEndorsements { get; set; }

    // Phase 2A
    public DbSet<WebOfWork.Api.Models.GlobalSettings> GlobalSettings { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all configurations from the assembly
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // Apply Global Soft Delete Query Filter
        var method = typeof(WebOfWorkDbContext).GetMethod(nameof(SetSoftDeleteFilter), BindingFlags.NonPublic | BindingFlags.Instance);
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
            {
                method?.MakeGenericMethod(entityType.ClrType).Invoke(this, new object[] { modelBuilder });
            }
        }
    }

    private void SetSoftDeleteFilter<TEntity>(ModelBuilder builder) where TEntity : class, ISoftDeletable
    {
        builder.Entity<TEntity>().HasQueryFilter(x => !x.IsDeleted);
    }

    public override int SaveChanges()
    {
        ApplyAuditRules();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditRules();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void ApplyAuditRules()
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.Entity is IAuditableEntity || e.Entity is ISoftDeletable);

        foreach (var entry in entries)
        {
            if (entry.Entity is IAuditableEntity auditable)
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        auditable.CreatedAt = DateTime.UtcNow;
                        auditable.UpdatedAt = DateTime.UtcNow;
                        break;
                    case EntityState.Modified:
                        auditable.UpdatedAt = DateTime.UtcNow;
                        break;
                }
            }

            if (entry.Entity is ISoftDeletable softDeletable && entry.State == EntityState.Deleted)
            {
                entry.State = EntityState.Modified;
                softDeletable.IsDeleted = true;
                softDeletable.DeletedAt = DateTime.UtcNow;
                
                // Also update UpdatedAt for auditable entities when soft deleting
                if (entry.Entity is IAuditableEntity auditableDeleted)
                {
                    auditableDeleted.UpdatedAt = DateTime.UtcNow;
                }
            }
        }
    }
}





