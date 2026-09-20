using System.Collections.Generic;

namespace WebOfWork.Api.Models;

public class Skill
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    
    public int? CategoryId { get; set; }
    public Category? Category { get; set; }
    
    public bool IsCustom { get; set; }
}
