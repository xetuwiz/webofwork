using System;
using WebOfWork.Api.Common.Interfaces;

namespace WebOfWork.Api.Models;

public class SkillEndorsement : IAuditableEntity
{
    public Guid Id { get; set; }
    public Guid SkillId { get; set; }
    public FreelancerSkill Skill { get; set; } = null!;
    
    public Guid EndorserId { get; set; }
    public User Endorser { get; set; } = null!;
    
    public string Comment { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
