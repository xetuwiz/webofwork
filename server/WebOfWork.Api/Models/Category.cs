using System.Collections.Generic;

namespace WebOfWork.Api.Models;

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public ICollection<Skill> Skills { get; set; } = new List<Skill>();
    public ICollection<FreelancerProfile> Profiles { get; set; } = new List<FreelancerProfile>();
}
