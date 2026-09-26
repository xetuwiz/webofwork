import React from 'react';
import { Briefcase, DollarSign, Clock, ExternalLink, Loader2 } from 'lucide-react';
import { useUpdateInvitationStatus } from './invitationMutations';
import Button from "../../../components/common/Button";

export function InvitationCard({ invitation }) {
  const { mutate: updateStatus, isPending } = useUpdateInvitationStatus();

  const handleAction = (status) => {
    updateStatus({ id: invitation.id, status });
  };

  const getStatusBadge = (status) => {
    const styles = {
      pending: "bg-surface-3 text-fg-muted",
      accepted: "bg-success-soft text-success",
      declined: "bg-danger-soft text-danger",
    };
    return (
      <span className={`inline-flex items-center px-3 py-1.5 rounded-full text-xs font-medium capitalize ${styles[status] || styles.pending}`}>
        {status}
      </span>
    );
  };

  return (
    <div className="bg-surface rounded-lg shadow-sm border border-line-soft p-6">
      <div className="flex flex-col sm:flex-row sm:items-start sm:justify-between gap-2 mb-2">
        <div className="flex items-center gap-2 text-xs text-fg-muted">
          <Briefcase className="w-4 h-4 text-fg-subtle" />
          <span>Invited by <span className="font-semibold">{invitation.client_name}</span></span>
        </div>
        <div className="self-start sm:self-auto">
          {getStatusBadge(invitation.status)}
        </div>
      </div>

      <h2 className="text-[17px] font-bold text-fg mb-2 leading-7">
        {invitation.project_title}
      </h2>

      <p className="text-xs text-fg-muted mb-4 leading-5 line-clamp-2">
        {invitation.message || "No message provided."}
      </p>

      <div className="flex items-center gap-6 mb-6">
        <div className="flex items-center gap-2">
          <span className="text-xs font-bold text-brand bg-brand-soft px-2 py-1 rounded">
            {invitation.package_type} Package
          </span>
        </div>
        <div className="flex items-center gap-2">
          <Clock className="w-4 h-4 text-fg-subtle" />
          <span className="text-xs text-fg-muted">
            Received {new Date(invitation.created_at).toLocaleDateString()}
          </span>
        </div>
      </div>

      <div className="flex flex-wrap items-center gap-3">
        {invitation.status === 'pending' && (
          <>
                        <Button
              onClick={() => handleAction('accepted')}
              variant="primary"
              size="none"
              loading={isPending}
              className="px-4 py-2.5 text-xs"
            >
              Accept Invitation
            </Button>
            <button 
              onClick={() => handleAction('declined')}
              disabled={isPending}
              className="px-4 py-2.5 rounded-lg border-2 border-danger/30 text-danger text-xs font-bold hover:bg-danger-soft disabled:opacity-50"
            >
              Decline
            </button>
          </>
        )}
        {invitation.status === 'accepted' && (
          <button className="px-4 py-2 rounded-lg bg-success-soft text-success text-xs font-bold flex items-center gap-2 cursor-default">
            Accepted
          </button>
        )}
      </div>
    </div>
  );
}
