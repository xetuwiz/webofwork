using System;

namespace WebOfWork.Api.Models;

public class PortfolioTag
{
    public Guid Id { get; set; }
    public Guid PortfolioItemId { get; set; }
    public FreelancerPortfolio PortfolioItem { get; set; } = null!;
    
    public Guid SkillId { get; set; }
    public FreelancerSkill Skill { get; set; } = null!;
}
