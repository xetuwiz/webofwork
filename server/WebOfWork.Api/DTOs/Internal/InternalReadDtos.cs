namespace WebOfWork.Api.DTOs.Internal;

public sealed record InternalAgentThresholdsDto(
    decimal MinBriefConfidence,
    decimal ProposalScreeningThreshold,
    decimal MatchScoreThreshold);

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
    Guid ClientId);

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
    IReadOnlyList<InternalPackageDto> Packages);

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
