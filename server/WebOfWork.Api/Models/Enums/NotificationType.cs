namespace WebOfWork.Api.Models.Enums;

public enum NotificationType
{
    WorkflowApprovalRequired,
    ProposalAccepted,
    InvitationReceived,
    OfferReceived,
    ProposalRejected,
    ContractStarted,
    WorkDelivered,
    RevisionRequested,
    RevisionAccepted,
    RevisionRejected,
    ContractCompleted,
    PaymentCredited,
    NewMessage,
    PayoutReleased,
    DisputeOpened,
    DisputeResolved,
    ReviewReceived,
    SupportReply
}
