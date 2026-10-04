using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace WebOfWork.Api.Data.EntityConfigurations;

public class MarketPriceReferenceConfiguration : IEntityTypeConfiguration<Models.MarketPriceReference>
{
    public void Configure(EntityTypeBuilder<Models.MarketPriceReference> builder)
    {
        builder.ToTable("market_price_references", t =>
        {
            // A band whose low end exceeds its high end is a typo, and a typo that
            // reads as a range is worse than a missing row: the drafter would quote
            // it and the client would post it. Rejected at the database rather than
            // trusted to the seed script and to every future admin edit.
            t.HasCheckConstraint(
                "ck_market_price_references_min_le_max",
                "min_price_usd <= max_price_usd");

            t.HasCheckConstraint(
                "ck_market_price_references_positive",
                "min_price_usd > 0 AND max_price_usd > 0");
        });

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Deliverable).IsRequired();
        builder.Property(r => r.Level).IsRequired();
        builder.Property(r => r.Source).IsRequired();

        // Money, not a float.
        builder.Property(r => r.MinPriceUsd).HasPrecision(18, 2);
        builder.Property(r => r.MaxPriceUsd).HasPrecision(18, 2);

        // One band per (deliverable, level, category). That triple is what
        // identifies a row, so it is the only thing that can carry uniqueness, and
        // it is what lets a corrected reseed be precise instead of guessing.
        //
        // Known and accepted: PostgreSQL treats NULLs as distinct in a unique
        // index, so the cross-cutting rows -- the ones with a null category_id --
        // are not constrained by it. A COALESCE-based index would cover them, but
        // EF cannot express one in the model, and hand-writing it in a migration
        // would put the snapshot out of step with the schema. The seed migration's
        // `WHERE NOT EXISTS` already compares with COALESCE, so the generated data
        // is protected; only a hand-inserted duplicate could slip through, and
        // `Down` deletes by `source` rather than by identity regardless.
        builder.HasIndex(r => new { r.Deliverable, r.Level, r.CategoryId })
            .IsUnique()
            .HasDatabaseName("ix_market_price_references_identity");

        builder.HasOne(r => r.Category)
               .WithMany()
               .HasForeignKey(r => r.CategoryId)
               // SetNull, not Cascade: a category does not own its bands. If an
               // admin removes one, its bands stay readable as generic rows rather
               // than vanishing from the agent's vocabulary.
               .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(r => r.CategoryId);
    }
}