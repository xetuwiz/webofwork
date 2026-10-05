import React, { useState } from 'react';
import { X, CheckCircle2, Trash2, AlertTriangle, Star } from 'lucide-react'; 
import { useModal } from '../../../../../hooks/modal/useModalStore';
import Dialog from '../../../../../components/common/Dialog';
import { Link, useNavigate } from 'react-router-dom';



export default function ProposalCard({ freelancer, proposal, isInvitation = false, invitationStatus = 'pending', anyOfferMade, onReject, isRejecting }) {
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [showConfirmReject, setShowConfirmReject] = useState(false);
  const { openModal } = useModal();
  const navigate = useNavigate();
  
  const contract = proposal.contract_info;
  const isHiredFreelancer = contract?.is_this_freelancer;
  const isRejected = invitationStatus === 'rejected';
  
  const MAX_LENGTH = 180; 
  const isLongMessage = proposal.description?.length > MAX_LENGTH;
  const displayDescription = isLongMessage 
    ? `${proposal.description.substring(0, MAX_LENGTH)}...` 
    : proposal.description;

  // A plain function returning JSX, not a component. It closes over
  // `freelancer`, so as a component it would have to be defined inside the
  // render -- which meant a new component type on every render, remounting the
  // subtree and tripping react-hooks/static-components. Called as
  // `{avatar(...)}` it is just an expression, with no identity to reconcile.
  const avatar = (size = "h-12 w-12", textSize = "text-xl") => (
    freelancer?.image ? (
      <img
        src={freelancer.image}
        referrerPolicy="no-referrer"
        className={`${size} rounded-full object-cover bg-surface-3 shadow-sm`}
        alt={freelancer.name}
      />
    ) : (
      <div className={`${size} rounded-full bg-brand flex items-center justify-center text-white font-bold ${textSize} shadow-sm`}>
        {freelancer?.name?.charAt(0).toUpperCase() || "U"}
      </div>
    )
  );

  const handleHire = () => {
    openModal('contract-offer', {
      projectName: proposal.title,
      freelancerName: freelancer.name,
      amount: proposal.bidAmount, 
      projectId: proposal.projectId,
      freelancerId: freelancer.id
    });
  };

  // These were nested components defined during render, which is both why
  // react-hooks/static-components flagged this file five times and why they
  // remounted (and lost their state) on every parent render. Inlined as
  // <Dialog>, they also gain the focus trap, Esc, scroll lock and aria-modal
  // that the hand-rolled `fixed inset-0 z-50` scrims never had.
  const fullProposalModal = (
    <Dialog
      open={isModalOpen}
      onClose={() => setIsModalOpen(false)}
      title="Full Proposal"
      size="xl"
      footer={
        <div className="flex justify-end">
          <button
            type="button"
            onClick={() => setIsModalOpen(false)}
            className="rounded-lg bg-brand px-6 py-2 text-sm font-medium text-brand-fg hover:bg-brand-hover/90"
          >
            Close
          </button>
        </div>
      }
    >
      <div className="flex items-center gap-4 mb-6">
        {avatar("h-14 w-14", "text-2xl")}
        <div>
          <Link to={`/find-talents/${freelancer.id}`}>
            <p className="font-bold text-fg hover:text-brand">{freelancer.name}</p>
            <p className="text-sm text-fg-subtle">{freelancer.title}</p>
          </Link>
        </div>
      </div>
      <p className="text-fg-muted leading-relaxed whitespace-pre-wrap">{proposal.description}</p>
    </Dialog>
  );

  const rejectConfirmModal = (
    <Dialog
      open={showConfirmReject}
      onClose={() => setShowConfirmReject(false)}
      title="Reject Proposal?"
      description={`Are you sure you want to reject ${freelancer.name}'s proposal? This action cannot be undone.`}
      size="sm"
      footer={
        <div className="flex justify-end gap-3">
          <button
            type="button"
            disabled={isRejecting}
            onClick={() => setShowConfirmReject(false)}
            className="rounded-lg px-4 py-2 text-sm font-medium text-fg-muted hover:bg-surface-3 transition"
          >
            Cancel
          </button>
          <button
            type="button"
            disabled={isRejecting}
            onClick={() => {
              onReject();
              setShowConfirmReject(false);
            }}
            className="rounded-lg bg-danger px-4 py-2 text-sm font-medium text-danger-fg hover:bg-danger transition"
          >
            {isRejecting ? "Rejecting..." : "Yes, Reject"}
          </button>
        </div>
      }
    >
      <div className="mb-4 flex h-12 w-12 items-center justify-center rounded-full bg-danger-soft">
        <AlertTriangle className="h-6 w-6 text-danger" aria-hidden="true" />
      </div>
    </Dialog>
  );

  if (isInvitation) {
    const statusConfig = {
      pending: { bg: "bg-warning-soft", text: "Invitation Pending" },
      accepted: { bg: "bg-success", text: "Invitation Accepted" },
      declined: { bg: "bg-danger-soft", text: "Invitation Declined" },
      hired: { bg: "bg-brand", text: "Hired" },
    };
    const currentStatus = statusConfig[invitationStatus] || statusConfig.pending;

    return (
      <div className={`relative rounded-lg border px-6 pb-6 pt-[25px] shadow-sm transition-all ${isHiredFreelancer ? 'border-blue-500 bg-brand-soft/40 ring-1 ring-blue-500' : 'border-line bg-surface'}`}>
        {isModalOpen && fullProposalModal}
        <div className="flex justify-between items-start mb-4">
          <span className={`inline-flex items-center rounded-full ${currentStatus.bg} px-3 py-1.5 text-xs font-medium text-white`}>
            {currentStatus.text}
          </span>
          
          {isHiredFreelancer ? (
            <button 
              onClick={() => navigate(`/contracts/${contract.id}`)}
              className="rounded-lg bg-brand px-4 py-1.5 text-xs font-bold text-brand-fg hover:bg-brand-hover transition"
            >
              View Contract
            </button>
          ) : (
            invitationStatus === 'accepted' && (
              <button 
                onClick={handleHire}
                disabled={anyOfferMade}
                className={`rounded-lg px-4 py-1.5 text-xs font-medium transition ${
                  anyOfferMade 
                  ? "bg-surface-3 text-fg-subtle cursor-not-allowed" 
                  : "bg-brand-soft text-white hover:bg-brand-hover"
                }`}
              >
                {anyOfferMade ? "Offer already out" : "Hire Now"}
              </button>
            )
          )}
        </div>

        <div className="flex items-start gap-4">
          {avatar("h-16 w-16", "text-2xl")}
          <div className="flex min-w-0 flex-col gap-1">
            <Link to={`/find-talents/${freelancer.id}`}>
              <h3 className="text-[15px] font-semibold text-fg hover:text-brand">{freelancer.name}</h3>
              <p className="text-xs font-medium text-fg-muted hover:text-brand">{freelancer.title}</p>
            </Link>
            <div className="mt-1">
              <p className="text-xs text-fg-subtle italic">"{displayDescription}"</p>
              {isLongMessage && (
                <button onClick={() => setIsModalOpen(true)} className="ml-1 text-xs font-semibold text-brand hover:underline">
                  Read more
                </button>
              )}
            </div>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className={`rounded-xl border p-6 shadow-sm transition-all ${isRejected ? 'opacity-60 bg-surface-2 grayscale-[0.5]' : ''} ${isHiredFreelancer ? 'border-blue-500 bg-brand-soft/40 ring-1 ring-blue-500' : 'border-line bg-surface'}`}>
      {isModalOpen && fullProposalModal}
      {showConfirmReject && rejectConfirmModal}
      
      {isHiredFreelancer && (
        <div className="mb-4 flex items-center gap-2 text-brand">
          <CheckCircle2 size={16} />
          <span className="text-xs font-bold uppercase tracking-wider">
            {contract?.status === 'active' ? 'Currently Hired' : 'Pending Offer'}
          </span>
        </div>
      )}

      {isRejected && (
        <div className="mb-4 flex items-center gap-2 text-danger">
          <X size={16} />
          <span className="text-xs font-bold uppercase tracking-wider">Rejected</span>
        </div>
      )}

      <div className="flex flex-col gap-6 lg:flex-row">
        <div className="flex gap-4">
          {avatar("h-[100px] w-[100px]", "text-4xl")}
          <div className="flex flex-col gap-1">
            <Link to={`/find-talents/${freelancer.id}` }>
            <h3 className="text-lg font-bold text-fg hover:text-brand">{freelancer.name}</h3>
            <p className="text-sm text-fg-subtle hover:text-brand">{freelancer.title}</p>
            </Link>
            <div className="flex items-center gap-1 pt-1">
              <Star className="w-4 h-4 fill-yellow-400 text-warning"/>
              <span className="ml-1 text-sm font-medium text-fg-muted">{freelancer.rating}</span>
            </div>
          </div>
        </div>

        <div className="flex flex-1 flex-col gap-4">
          <div className="flex flex-col lg:flex-row lg:justify-between gap-2">
            <h4 className="text-base font-semibold text-fg">Proposal Details</h4>
            <div className="flex gap-4">
              <div className="lg:text-right">
                <p className="text-[10px] font-bold uppercase tracking-wider text-fg-subtle">Bid Amount</p>
                <p className="text-base font-semibold text-fg">${proposal.bidAmount}</p>
              </div>
              <div className="lg:text-right">
                <p className="text-[10px] font-bold uppercase tracking-wider text-fg-subtle">Delivery</p>
                <p className="text-base font-semibold text-fg">{proposal.deliveryDays} Days</p>
              </div>
            </div>
          </div>
          <div>
            <p className="text-sm text-fg-muted leading-relaxed inline">{displayDescription}</p>
            {isLongMessage && (
              <button onClick={() => setIsModalOpen(true)} className="ml-2 text-sm font-semibold text-brand hover:text-brand underline underline-offset-2">
                View full proposal
              </button>
            )}
          </div>
        </div>
      </div>

      <div className="mt-6 flex flex-col-reverse gap-3 sm:flex-row sm:justify-end border-t pt-6">
        {!isHiredFreelancer && !isRejected && (
          <button 
            onClick={() => setShowConfirmReject(true)}
            className="flex items-center justify-center gap-2 rounded-lg border border-line bg-surface px-6 py-2 text-sm font-medium text-fg-muted hover:bg-danger-soft hover:text-danger hover:border-danger/30 transition"
          >
            <Trash2 size={16} />
            Reject
          </button>
        )}

        {isHiredFreelancer ? (
          <button 
            onClick={() => navigate(`/contracts/${contract.id}`)}
            className="rounded-lg bg-brand px-8 py-2.5 text-sm font-bold text-brand-fg hover:bg-brand-hover transition shadow-md"
          >
            View Contract
          </button>
        ) : (
          !isRejected && (
            <button 
              onClick={handleHire} 
              disabled={anyOfferMade}
              className={`rounded-lg px-6 py-2 text-sm font-medium transition ${
                anyOfferMade 
                ? "bg-surface-3 text-fg-subtle cursor-not-allowed border border-line" 
                : "bg-brand-soft text-white hover:bg-brand-hover"
              }`}
            >
              {anyOfferMade ? "Offer already out" : "Hire Freelancer"}
            </button>
          )
        )}
      </div>
    </div>
  );
}
