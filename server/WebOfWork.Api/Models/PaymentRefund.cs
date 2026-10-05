using System;
using System.Collections.Generic;

namespace WebOfWork.Api.Models;

public class PaymentRefund
{
    public int Id { get; set; }
    public int PaymentId { get; set; }
    public Payment Payment { get; set; } = null!;
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
