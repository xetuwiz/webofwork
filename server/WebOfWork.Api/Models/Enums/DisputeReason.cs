using System;

namespace WebOfWork.Api.Models.Enums;

public enum DisputeReason
{
    ScopeCreep = 1,
    NoFeedback = 2,
    UnlimitedRevisions = 3,
    PoorQuality = 4,
    MissedDeadline = 5,
    IncompleteWork = 6,
    CommunicationIssue = 7,
    Other = 8
}
