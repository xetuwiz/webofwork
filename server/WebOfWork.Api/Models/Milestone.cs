using System;
using System.Collections.Generic;
using WebOfWork.Api.Models.Enums;

namespace WebOfWork.Api.Models;

public class Milestone
{
    public int Id { get; set; }
    
    public int ContractId { get; set; }
    public Contract Contract { get; set; } = null!;

    public int Order { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int ExpectedDays { get; set; }

    public MilestoneStatus Status { get; set; } = MilestoneStatus.Pending;

    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? FundedAt { get; set; }

    public List<MilestoneDeliverable> Deliverables { get; set; } = new();
}
