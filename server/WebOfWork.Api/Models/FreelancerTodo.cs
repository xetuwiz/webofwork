using System;

namespace WebOfWork.Api.Models;

public class FreelancerTodo
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    
    public string Todo { get; set; } = string.Empty;
    public bool IsCompleted { get; set; } = false;
}
