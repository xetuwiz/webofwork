namespace WebOfWork.Api.DTOs.Internal;

public sealed record InternalAgentThresholdsDto(
    decimal MinBriefConfidence,
    decimal ProposalScreeningThreshold,
    decimal MatchScoreThreshold);

/// <summary>
/// A project, as the agent service sees it.
/// </summary>
/// <remarks>
/// <c>CategoryId</c> and <c>Skills</c> were added because two agents could not
/// work without them and were silently degrading:
/// <list type="bullet">
/// <item>the draft agent has to ground its <c>category_id</c> and
/// <c>skill_ids</c> in real rows, and the project it was drafting is the best
/// available source of "which skills does this kind of work need";</item>
/// <item>the matching agent scores skill coverage, and with no skill list on
/// either side it had nothing to compare.</item>
/// </list>
/// Additive, so the Python side can read the fields and an older agent service
/// simply ignores them.
/// </remarks>
/// <summary>
  /// One comparable project, for the planner's budget-and-timeline sanity check.
  /// </summary>
  /// <remarks>
  /// Deliberately narrower than <see cref="InternalProjectDto"/>. This exists so
  /// the agent can answer "what does a normal budget for this kind of work look
  /// like", and every field here is one that answers that. A client's email, a
  /// full requirements document and a free-text description would be more
  /// faithful and are not wanted: the planner needs a distribution, not the
  /// contents of someone else's brief.
  ///
  /// Carrying the pricing triple rather than a single "budget" number is
  /// deliberate. A project is either fixed-price or a range, and collapsing the
  /// two would make the median meaningless across a mixed set.
  /// </remarks>
  public sealed record InternalSimilarProjectDto(
      int Id,
      string Title,
      string PricingType,
      decimal? FixedPrice,
      decimal? MinPrice,
      decimal? MaxPrice,
      int ExpectedDays,
      string Status,
      DateTime CreatedAt);

  public sealed record InternalProjectDto(
    int Id,
    string Title,
    string Description,
    string Requirements,
    string ExpectedDeliverables,
    string PricingType,
    decimal? FixedPrice,
    decimal? MinPrice,
    decimal? MaxPrice,
    int ExpectedDays,
    string Status,
    Guid ClientId,
    int? CategoryId,
    IReadOnlyList<string> Skills);

public sealed record InternalFreelancerListDto(
    Guid Id,
    string FullName,
    string Tagline,
    string Bio,
    int? CategoryId,
    string ExperienceLevel,
    bool Availability,
    decimal AverageRating,
    int ReviewCount,
    IReadOnlyList<string> Skills,
    IReadOnlyList<InternalPackageDto> Packages,
    /// <summary>
    /// The candidate's portfolio entries.
    /// </summary>
    /// <remarks>
    /// Present because <c>portfolio_relevance</c> is one of the five scoring
    /// weights the client displays and lets a person adjust, and it is measured as
    /// term overlap between the brief and this text. Without it the agent read
    /// <c>candidate.get("portfolio")</c>, got <c>None</c>, and scored the weight
    /// from packages alone -- so a freelancer whose portfolio matched the brief
    /// scored as though they had none.
    ///
    /// It was omitted on the reasoning that the detail endpoint already returns
    /// portfolio data and the list endpoint's job is to be cheap. That reasoning is
    /// sound until you notice the agent never calls the detail endpoint for the
    /// pool: the matching stage reads one list and scores everyone in it.
    /// </remarks>
    IReadOnlyList<InternalPortfolioDto> Portfolio);

public sealed record InternalFreelancerProfileDto(
    Guid Id,
    string FullName,
    string Tagline,
    string Bio,
    string ExperienceLevel,
    bool Availability,
    bool IsPhoneVerified,
    decimal AverageRating,
    int ReviewCount,
    IReadOnlyList<string> Skills,
    IReadOnlyList<InternalPackageDto> Packages,
    IReadOnlyList<InternalPortfolioDto> Portfolio);

public sealed record InternalPackageDto(
    Guid Id,
    string PackageType,
    decimal PriceUsd,
    int DeliveryDays,
    string Description);

public sealed record InternalPortfolioDto(
    Guid Id,
    string Title,
    string Description,
    string ImageUrl);

public sealed record InternalReviewDto(
    int Id,
    int ContractId,
    Guid ReviewerId,
    int Rating,
    string Comment,
    DateTime CreatedAt);

public sealed record InternalCommissionDto(decimal CommissionPercentage, string Currency);

/// <summary>
/// One advertised reference price band, as the drafting agent reads it.
/// </summary>
/// <remarks>
/// <para>
/// <c>Source</c> is carried on every row rather than once in a header, because the
/// agent has to be able to say where a number came from when a client asks. A
/// price the client cannot trace is a price they will not post.
/// </para>
/// <para>
/// The field names describe bands as bands. There is no <c>Price</c> and no
/// <c>RecommendedPrice</c>, on purpose: a singular field is what a caller reaches
/// for when it wants a number to quote, and this data cannot support that.
/// </para>
/// </remarks>
/// <param name="Deliverable">What is being priced, in client vocabulary.</param>
/// <param name="Level">Coarse experience band the figure assumes.</param>
/// <param name="MinPriceUsd">Low end of the advertised band.</param>
/// <param name="MaxPriceUsd">High end of the advertised band.</param>
/// <param name="CategoryId">Owning category, or null for "any category".</param>
/// <param name="CategoryName">Denormalised so the agent never needs a second call.</param>
public sealed record InternalPriceBandDto(
    string Deliverable,
    string Level,
    decimal MinPriceUsd,
    decimal MaxPriceUsd,
    int? TypicalDaysMin,
    int? TypicalDaysMax,
    int? CategoryId,
    string? CategoryName,
    string Source);

/// <summary>One real catalogue skill, for the drafting agent to ground against.</summary>
/// <remarks>
/// <paramref name="CategoryId"/> is not redundant with the route. A skill row and a
/// category row are both <c>{id, name}</c>, so the agent's grounding can only tell
/// them apart by the category field -- and it has to be on every row for that check
/// to work when skills arrive as a flat list rather than nested.
/// </remarks>
/// <param name="Id">Catalogue id. The only thing a posted project can reference.</param>
/// <param name="Name">Display name, as the client's form shows it.</param>
/// <param name="CategoryId">Owning category.</param>
/// <param name="IsCustom">True when a user added it rather than it being seeded.</param>
public sealed record InternalSkillDto(
    int Id,
    string Name,
    int? CategoryId,
    bool IsCustom);

public sealed record InternalDisputeDto(
    int Id,
    int ContractId,
    string Reason,
    string Description,
    string Status,
    string? Resolution,
    DateTime CreatedAt);

public sealed record InternalBriefDto(
    int Id,
    string Text,
    decimal? BudgetHint,
    DateTime? DeadlineHint,
    string Status,
    InternalBriefAnalysisDto? Analysis);

public sealed record InternalBriefAnalysisDto(
    int Id,
    IReadOnlyList<string> RequiredSkills,
    string Complexity,
    decimal SuggestedBudgetUsd,
    string Timeline,
    decimal Confidence);
