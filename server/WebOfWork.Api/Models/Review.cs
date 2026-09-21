using System;
using WebOfWork.Api.Models.Enums;

namespace WebOfWork.Api.Models;

public class Review
{
    public int Id { get; set; }

    public int ContractId { get; set; }
    public Contract Contract { get; set; } = null!;

    public Guid ReviewerId { get; set; }
    public User Reviewer { get; set; } = null!;

    public Guid RevieweeId { get; set; }
    public User Reviewee { get; set; } = null!;

    public ReviewType Type { get; set; }
    public int Rating { get; set; }
    public string Comment { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
