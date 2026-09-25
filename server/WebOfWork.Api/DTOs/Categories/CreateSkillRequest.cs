namespace WebOfWork.Api.DTOs.Categories;

public class CreateSkillRequest
{
    public string Name { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public int Category { get; set; }
    public bool IsCustom { get; set; }
}
