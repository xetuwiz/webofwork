using System;

namespace WebOfWork.Api.Models;

public class FreelancerPackage
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    
    public string PackageType { get; set; } = string.Empty; // starter, pro
    public decimal Price { get; set; }
    public int DeliveryDays { get; set; }
    public string Description { get; set; } = string.Empty;
}
