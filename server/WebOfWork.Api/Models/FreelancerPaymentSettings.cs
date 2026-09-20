using System;

namespace WebOfWork.Api.Models;

public class FreelancerPaymentSettings
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    
    public string PaypalEmail { get; set; } = string.Empty;
    public bool IsVerified { get; set; } = true;
}
