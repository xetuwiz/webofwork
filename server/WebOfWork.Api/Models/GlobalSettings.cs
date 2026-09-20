using WebOfWork.Api.Common.Interfaces;

namespace WebOfWork.Api.Models;

public class GlobalSettings : IAuditableEntity
{
    public int Id { get; set; }
    
    public string SupportEmail { get; set; } = "support@webofwork.lk";
    
    // CommissionPercentage, MinBriefConfidence, ProposalScreeningThreshold, MatchScoreThreshold
    // are forced to decimal(18,2) in the configuration.
    public decimal CommissionPercentage { get; set; } = 10.00m;
    
    public string PayPalEmail { get; set; } = "payments@webofwork.lk";
    
    public decimal MinBriefConfidence { get; set; } = 0.60m;
    
    public decimal ProposalScreeningThreshold { get; set; } = 0.50m;
    
    public decimal MatchScoreThreshold { get; set; } = 0.60m;
    
    public string DefaultCurrency { get; private set; } = "LKR"; // Private setter to enforce immutability at the domain level

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
