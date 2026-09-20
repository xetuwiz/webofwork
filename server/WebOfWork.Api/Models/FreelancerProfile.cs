using System;
using System.Collections.Generic;

namespace WebOfWork.Api.Models;

public class FreelancerProfile
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    
    public string Tagline { get; set; } = string.Empty;
    public string Bio { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public bool IsPhoneVerified { get; set; } = false;
    
    public decimal AverageRating { get; set; } = 0.00m;
    public int ReviewCount { get; set; } = 0;
    
    public int? CategoryId { get; set; }
    public string ExperienceLevel { get; set; } = "beginner";
    public bool Availability { get; set; } = true;
    public bool IsActive { get; set; } = false;
    
    public decimal TotalEarnings { get; set; } = 0.00m;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
