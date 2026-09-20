using System;

namespace WebOfWork.Api.DTOs.Portfolio;

public record SkillEndorsementResponse(
    Guid Id,
    Guid SkillId,
    Guid EndorserId,
    string EndorserName,
    string Comment,
    DateTime CreatedAt
);

public class CreateSkillEndorsementRequest
{
    public Guid SkillId { get; set; }
    public string Comment { get; set; } = string.Empty;
}
