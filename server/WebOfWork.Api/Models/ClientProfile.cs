using System;
using System.Collections.Generic;

namespace WebOfWork.Api.Models;

public class ClientProfile
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    
    public string CompanyName { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    
    public decimal AverageRating { get; set; } = 0.00m;
    public int ReviewCount { get; set; } = 0;
    
    public decimal TotalSpent { get; set; } = 0.00m;
    public int TotalProjectsPosted { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
