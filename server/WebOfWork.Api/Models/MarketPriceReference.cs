using System;

namespace WebOfWork.Api.Models;

/// <summary>
/// A published reference price band for one kind of work.
/// </summary>
/// <remarks>
/// <para>
/// This exists because the draft agent had no way to answer "what does this
/// usually cost?" and so declined to. A client who posts a brief with no budget
/// got <c>"no price is suggested"</c> and a shrug, which is the least useful
/// output a drafting assistant can produce: the client now has to research their
/// own pricing before they can use the thing.
/// </para>
/// <para>
/// It is <b>reference data, not a quote</b>, and the naming is deliberate. These
/// are advertised "typical" rates collected from a marketplace's public category
/// pages, which is the same category of number a marketplace shows a seller as a
/// guide. Observed freelancer profiles on that marketplace span roughly $3/hr to
/// $85/hr, well outside the stated bands, so a row here must never be presented as
/// what the work costs -- only as a defensible starting point the client adjusts.
/// Anything that says "market rate" is overstating the source.
/// </para>
/// <para>
/// Deliberately read-only and deliberately not connected to
/// <see cref="FreelancerPackage"/>. A package is what a freelancer chose to
/// sell; a reference band is what comparable work is advertised at. Deriving one
/// from the other would let the cheapest listed package rewrite the reference for
/// everyone, which is how a benchmark stops being a benchmark.
/// </para>
/// </remarks>
public class MarketPriceReference
{
    public int Id { get; set; }

    /// <summary>
    /// Owning category, resolved by name at seed time.
    /// </summary>
    /// <remarks>
    /// Nullable so the table can be seeded before -- or entirely without --
    /// categories. A null means "any category", which is how the handful of
    /// genuinely cross-cutting rows (an AI readiness audit, say) stay usable
    /// rather than being forced into an arbitrary bucket.
    /// </remarks>
    public int? CategoryId { get; set; }

    public Category? Category { get; set; }

    /// <summary>
    /// What is being priced, in the words a client would use: "AI chatbot",
    /// "Landing page design". Not a skill id -- a client does not know the skill
    /// taxonomy, and a row keyed by it would be unfindable by the only party
    /// meant to read it.
    /// </summary>
    public string Deliverable { get; set; } = string.Empty;

    /// <summary>
    /// Coarse experience band: entry, intermediate, expert, or a compound like
    /// "intermediate-expert".
    /// </summary>
    /// <remarks>
    /// A closed-ish vocabulary in practice, but stored as text because it is a
    /// label, not a behaviour. A hard enum would make "entry-mid" -- a real and
    /// useful distinction the source data makes -- unrepresentable.
    /// </remarks>
    public string Level { get; set; } = string.Empty;

    /// <summary>Low end of the advertised band, in USD.</summary>
    public decimal MinPriceUsd { get; set; }

    /// <summary>High end of the advertised band, in USD.</summary>
    public decimal MaxPriceUsd { get; set; }

    /// <summary>How long comparable work takes at this level. Null when unknown.</summary>
    public int? TypicalDaysMin { get; set; }

    public int? TypicalDaysMax { get; set; }

    /// <summary>
    /// Where the band came from, kept so it can be shown to a client.
    /// </summary>
    /// <remarks>
    /// Not decoration. An unsourced number shown next to a client's budget
    /// invites the question "who says so?", and "the platform's reference
    /// catalogue" is an answer while a bare figure is not.
    /// </remarks>
    public string Source { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}