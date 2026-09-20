using System;

namespace WebOfWork.Api.Models;

public class BriefAttachment
{
    public int Id { get; set; }
    public int BriefId { get; set; }
    public Brief Brief { get; set; } = null!;
    
    public string FileUrl { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
