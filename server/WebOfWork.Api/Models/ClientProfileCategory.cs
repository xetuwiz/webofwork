namespace WebOfWork.Api.Models;

public class ClientProfileCategory
{
    public Guid ClientProfileId { get; set; }
    public ClientProfile ClientProfile { get; set; } = null!;
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
}
