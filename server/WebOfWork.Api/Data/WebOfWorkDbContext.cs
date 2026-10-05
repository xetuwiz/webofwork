using System.Reflection;
using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;
using WebOfWork.Api.Common.Interfaces;
using WebOfWork.Api.Models;

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
    public DbSet<WebOfWork.Api.Models.MarketPriceReference> MarketPriceReferences { get; set; }
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
    public DbSet<WebOfWork.Api.Models.Dispute> Disputes { get; set; }
    public DbSet<WebOfWork.Api.Models.DisputeFile> DisputeFiles { get; set; }
    public DbSet<WebOfWork.Api.Models.Review> Reviews { get; set; }
    public DbSet<WebOfWork.Api.Models.Payment> Payments { get; set; }
    public DbSet<WebOfWork.Api.Models.PaymentRefund> PaymentRefunds { get; set; }

    public DbSet<WebOfWork.Api.Models.PlatformAnalytics> PlatformAnalytics { get; set; }
    public DbSet<WebOfWork.Api.Models.FreelancerSkill> FreelancerSkills { get; set; }
    public DbSet<WebOfWork.Api.Models.FreelancerPackage> FreelancerPackages { get; set; }
    public DbSet<WebOfWork.Api.Models.FreelancerPortfolio> FreelancerPortfolios { get; set; }
    public DbSet<WebOfWork.Api.Models.FreelancerPaymentSettings> FreelancerPaymentSettings { get; set; }
    public DbSet<WebOfWork.Api.Models.FreelancerTodo> FreelancerTodos { get; set; }

    // Phase 2A
    public DbSet<WebOfWork.Api.Models.PortfolioMedia> PortfolioMedia { get; set; }

    // Phase 2A
    public DbSet<WebOfWork.Api.Models.GlobalSettings> GlobalSettings { get; set; }

    // Phase 14
    public DbSet<WebOfWork.Api.Models.ChatRoom> ChatRooms { get; set; }
    public DbSet<WebOfWork.Api.Models.Message> Messages { get; set; }
    public DbSet<WebOfWork.Api.Models.Notification> Notifications { get; set; }

    // Phase 15
    public DbSet<WebOfWork.Api.Models.SupportTicket> SupportTickets { get; set; }
    public DbSet<WebOfWork.Api.Models.Faq> Faqs { get; set; }
    public DbSet<WebOfWork.Api.Models.CmsSection> CmsSections { get; set; }

    // Phase 18A
    public DbSet<WebOfWork.Api.Models.WorkflowRun> WorkflowRuns { get; set; }
    public DbSet<WebOfWork.Api.Models.WorkflowStepLog> WorkflowStepLogs { get; set; }
    public DbSet<WebOfWork.Api.Models.ToolCallLog> ToolCallLogs { get; set; }
    public DbSet<WebOfWork.Api.Models.AgentRecommendation> AgentRecommendations { get; set; }
    public DbSet<WebOfWork.Api.Models.ProposalScreening> ProposalScreenings { get; set; }
    public DbSet<WebOfWork.Api.Models.ValidationResult> ValidationResults { get; set; }

    // Push + per-user delivery preferences
    public DbSet<WebOfWork.Api.Models.NotificationPreference> NotificationPreferences { get; set; }
    public DbSet<WebOfWork.Api.Models.NotificationTypePreference> NotificationTypePreferences { get; set; }
    public DbSet<WebOfWork.Api.Models.DeviceRegistration> DeviceRegistrations { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all configurations from the assembly
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // Only when the column can actually exist. See the method for why.
        if (Database.IsNpgsql())
        {
            MapSearchVector(modelBuilder);
        }

        // Apply Global Soft Delete Query Filter
        var method = typeof(WebOfWorkDbContext).GetMethod(nameof(SetSoftDeleteFilter), BindingFlags.NonPublic | BindingFlags.Instance);
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
            {
                method?.MakeGenericMethod(entityType.ClrType).Invoke(this, new object[] { modelBuilder });
            }
        }

        ApplySoftDeletedUserFilters(modelBuilder);
        MapSearchFunctions(modelBuilder);
    }

    /// <summary>
    /// Registers <c>projects.search_vector</c> as a read-only shadow property.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The column is <c>GENERATED ALWAYS AS ... STORED</c>, so PostgreSQL owns it and
    /// EF has no reason to know about it. But <c>EF.Property&lt;T&gt;</c> validates
    /// the name against the model and fails with "the specified property does not
    /// exist on the entity type" for anything unregistered -- which makes full-text
    /// search impossible to express in LINQ without this.
    /// </para>
    /// <para>
    /// A <b>shadow property</b> rather than a CLR property on <c>Project</c>, because:
    /// </para>
    /// <list type="bullet">
    /// <item>It is never populated, so a CLR property would always read
    /// <c>null</c> and invite code that trusts it.</item>
    /// <item>It is not writable. <c>ValueGeneratedOnAddOrUpdate</c> with
    /// <c>HasColumnType("tsvector")</c> marks it computed, so an accidental
    /// assignment in an <c>UPDATE</c> is refused by the database rather than
    /// silently ignored -- which is what <c>GENERATED ALWAYS</c> guarantees.</item>
    /// </list>
    /// <para>
    /// The trade is explicit: EF now believes a column exists, so a future migration
    /// that drops it will fail loudly at query time rather than quietly. That is the
    /// direction you want a mistake to fail in.
    /// </para>
    /// <para>
    /// <b>Guarded by <c>Database.IsNpgsql()</c>.</b> A few tests build the model with
    /// <c>EnsureCreated</c> on a provider that has no <c>tsvector</c> type, and EF
    /// refuses to build a model at all when a property type is unknown to the
    /// provider: "The 'NpgsqlTsVector' property 'Project.search_vector' could not be
    /// mapped because the database provider does not support this type". That
    /// surfaced as 42 unrelated test failures across approval gates, workflows and
    /// the read service -- all of them failing at model construction, none of them
    /// mentioning search.
    /// </para>
    /// </remarks>
    private static void MapSearchVector(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Project>()
            .Property<NpgsqlTsVector>("search_vector")
            .HasColumnType("tsvector")
            .HasColumnName("search_vector")
            // Computed by the database. EF will read it in a SELECT and will not
            // attempt to write it in an INSERT or UPDATE, which matters because the
            // database rejects any value for a GENERATED ALWAYS column -- so this is
            // not merely tidy, it is what stops an INSERT from failing.
            .ValueGeneratedOnAddOrUpdate();
    }

    /// <summary>
    /// Maps <c>ts_rank_cd</c> so relevance ordering can be expressed in LINQ.
    /// </summary>
    /// <remarks>
    /// The Npgsql provider translates <c>@@</c> but not <c>ts_rank_cd</c>, so without
    /// this the only ways to order by relevance are to materialise every matching row
    /// and sort in C#, or to hand-write the whole query as raw SQL. Both give up the
    /// GIN index doing the filtering. Mapping the function name is the smallest
    /// change that keeps ranking inside the database.
    /// </remarks>
    private static void MapSearchFunctions(ModelBuilder modelBuilder)
    {
        // Parameters are bound positionally from the CLR signature, so there is no
        // `HasArgument` call: the names exist only for readability in the signature.
        //
        // Note this maps `ts_rank_cd`, not `websearch_to_tsquery`. The provider
        // already translates `@@` from a plain search string, and it does so by
        // wrapping the value in `plainto_tsquery` internally. Mapping a second copy
        // of that conversion and calling it explicitly does not translate at all,
        // because `Matches` is a leaf method with no expression underneath it to
        // translate.
        modelBuilder
            .HasDbFunction(typeof(SearchFunctions).GetMethod(nameof(SearchFunctions.Rank))!)
            .HasName("ts_rank_cd");
    }

    /// <summary>
    /// <see cref="User"/> is soft-deleted via a global query filter, while dependent
    /// entities declare a required <c>User</c> navigation. EF Core warns that the
    /// required principal may be filtered out, which can yield inconsistent results.
    ///
    /// The FK columns are non-nullable, so the relationship is genuinely required and
    /// cannot simply be made optional. Instead we add a matching filter to every
    /// dependent entity so a soft-deleted user hides its dependents consistently:
    /// the dependent is returned if and only if its user is not soft-deleted.
    /// </summary>
    private static void ApplySoftDeletedUserFilters(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ClientProfile>().HasQueryFilter(c => !c.User.IsDeleted);
        modelBuilder.Entity<FreelancerProfile>().HasQueryFilter(f => !f.User.IsDeleted);
        modelBuilder.Entity<FreelancerPackage>().HasQueryFilter(p => !p.User.IsDeleted);
        modelBuilder.Entity<FreelancerPaymentSettings>().HasQueryFilter(s => !s.User.IsDeleted);
        modelBuilder.Entity<FreelancerPortfolio>().HasQueryFilter(p => !p.User.IsDeleted);
        modelBuilder.Entity<FreelancerSkill>().HasQueryFilter(s => !s.User.IsDeleted);
        modelBuilder.Entity<FreelancerTodo>().HasQueryFilter(t => !t.User.IsDeleted);
        modelBuilder.Entity<ContractRevision>().HasQueryFilter(r => !r.User.IsDeleted);
        modelBuilder.Entity<Dispute>().HasQueryFilter(d => !d.Opener.IsDeleted);
        modelBuilder.Entity<Notification>().HasQueryFilter(n => !n.Recipient.IsDeleted);
        modelBuilder.Entity<Review>().HasQueryFilter(r => !r.Reviewer.IsDeleted && !r.Reviewee.IsDeleted);
        modelBuilder.Entity<SupportTicket>().HasQueryFilter(t => !t.User.IsDeleted);
        modelBuilder.Entity<WorkflowRun>().HasQueryFilter(w => !w.RequestedByUser.IsDeleted);
        // Notification preferences and device registrations. Without these, a
        // soft-deleted user's preferences and device tokens would keep being read
        // and written, and EF logs a model warning for each.
        modelBuilder.Entity<NotificationPreference>().HasQueryFilter(p => !p.User.IsDeleted);
        modelBuilder.Entity<NotificationTypePreference>().HasQueryFilter(t => !t.User.IsDeleted);
        modelBuilder.Entity<DeviceRegistration>().HasQueryFilter(d => !d.User.IsDeleted);

        // Second tier: entities whose required principal is itself filtered above.
        // These entities have no IsDeleted column, so the filter is expressed
        // against the originating user through the (filtered) principal navigation.
        modelBuilder.Entity<Brief>().HasQueryFilter(b => !b.Client.User.IsDeleted);
        modelBuilder.Entity<ClientProfileCategory>().HasQueryFilter(l => !l.ClientProfile.User.IsDeleted);
        modelBuilder.Entity<Contract>().HasQueryFilter(c => !c.Client.User.IsDeleted && !c.Freelancer.User.IsDeleted);
        modelBuilder.Entity<Invitation>().HasQueryFilter(i => !i.Client.User.IsDeleted && !i.Freelancer.User.IsDeleted);
        modelBuilder.Entity<Project>().HasQueryFilter(p => !p.Client.User.IsDeleted);
        modelBuilder.Entity<ContractDeliverable>().HasQueryFilter(d => !d.Freelancer.User.IsDeleted);
        modelBuilder.Entity<Proposal>().HasQueryFilter(p => !p.Freelancer.User.IsDeleted);
        modelBuilder.Entity<DisputeFile>().HasQueryFilter(f => !f.Dispute.Opener.IsDeleted);
        modelBuilder.Entity<PortfolioMedia>().HasQueryFilter(m => !m.PortfolioItem.User.IsDeleted);
        modelBuilder.Entity<AgentRecommendation>().HasQueryFilter(a => !a.WorkflowRun.RequestedByUser.IsDeleted);
        modelBuilder.Entity<ProposalScreening>().HasQueryFilter(s => !s.WorkflowRun.RequestedByUser.IsDeleted);
        modelBuilder.Entity<ToolCallLog>().HasQueryFilter(t => !t.WorkflowRun.RequestedByUser.IsDeleted);
        modelBuilder.Entity<ValidationResult>().HasQueryFilter(v => !v.WorkflowRun.RequestedByUser.IsDeleted);
        modelBuilder.Entity<WorkflowStepLog>().HasQueryFilter(l => !l.WorkflowRun.RequestedByUser.IsDeleted);

        // Third tier: children of the second-tier filtered entities.
        modelBuilder.Entity<BriefAnalysis>().HasQueryFilter(a => !a.Brief.Client.User.IsDeleted);
        modelBuilder.Entity<BriefAttachment>().HasQueryFilter(a => !a.Brief.Client.User.IsDeleted);
        modelBuilder.Entity<Payment>().HasQueryFilter(p => !p.Contract.Client.User.IsDeleted && !p.Contract.Freelancer.User.IsDeleted);
        modelBuilder.Entity<ProjectSkill>().HasQueryFilter(s => !s.Project.Client.User.IsDeleted);

        // Fourth tier: children of Payment.
        modelBuilder.Entity<PaymentRefund>().HasQueryFilter(r => !r.Payment.Contract.Client.User.IsDeleted && !r.Payment.Contract.Freelancer.User.IsDeleted);
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







