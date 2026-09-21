using System;

namespace WebOfWork.Api.Models;

public class DisputeFile
{
    public int Id { get; set; }

    public int DisputeId { get; set; }
    public Dispute Dispute { get; set; } = null!;

    public string FileUrl { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
