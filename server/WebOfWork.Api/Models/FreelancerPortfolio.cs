using System;
using System.Collections.Generic;

namespace WebOfWork.Api.Models;

public class FreelancerPortfolio
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<PortfolioMedia> Media { get; set; } = new List<PortfolioMedia>();
}
