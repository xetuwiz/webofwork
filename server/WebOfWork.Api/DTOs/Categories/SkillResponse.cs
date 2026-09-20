namespace WebOfWork.Api.DTOs.Categories;

public class SkillResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public bool IsCustom { get; set; }
}
