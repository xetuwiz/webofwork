namespace WebOfWork.Api.DTOs.Revisions;

public record RevisionActionRequest(
    bool Accept,
    string? RejectionMessage
);
