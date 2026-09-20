using System;
using WebOfWork.Api.Models.Enums;
using WebOfWork.Api.Common.Interfaces;

namespace WebOfWork.Api.Models;

public class PortfolioMedia : IAuditableEntity
{
    public Guid Id { get; set; }
    public Guid PortfolioItemId { get; set; }
    public FreelancerPortfolio PortfolioItem { get; set; } = null!;
    
    public MediaType MediaType { get; set; }
    public string Url { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
    public string Caption { get; set; } = string.Empty;
    public int OrderIndex { get; set; }
    public string PublicId { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
