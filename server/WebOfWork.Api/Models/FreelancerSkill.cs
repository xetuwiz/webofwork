using System;
using System.Collections.Generic;

namespace WebOfWork.Api.Models;

public class FreelancerSkill
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public int? SkillId { get; set; }
    public string CustomName { get; set; } = string.Empty;

}
