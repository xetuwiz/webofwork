import React from "react";
import { X, Calendar, Clock, Banknote } from "lucide-react";
import Dialog from "../../../../components/common/Dialog";

export default function ProposalDetailModal({ isOpen, onClose, proposal }) {
  if (!isOpen || !proposal) return null;

  return (
    <Dialog
      open={isOpen}
      onClose={onClose}
      title="Proposal Details"
      size="md"
      className="overflow-hidden"
    >
        <div className="flex items-center justify-between border-b px-6 py-4">
          <h3 className="text-xl font-bold text-fg">Proposal Details</h3>
          <button onClick={onClose} className="p-2 hover:bg-surface-3 rounded-full transition-colors">
            <X className="w-6 h-6 text-fg-subtle" />
          </button>
        </div>

        <div className="p-6 overflow-y-auto max-h-[80vh]">
          <div className="mb-6">
            <h4 className="text-sm font-bold text-brand uppercase tracking-wider mb-1">Project</h4>
            <p className="text-lg font-semibold text-fg">{proposal.project_title}</p>
            <p className="text-sm text-fg-subtle">Client: {proposal.client_name}</p>
          </div>

          <div className="grid grid-cols-3 gap-4 mb-8 p-4 bg-surface-2 rounded-xl">
            <div>
              <div className="flex items-center gap-2 text-fg-subtle mb-1">
                <Banknote className="w-4 h-4" />
                <span className="text-xs font-medium">Bid Amount</span>
              </div>
              <p className="font-bold text-fg">${Number(proposal.bid_amount).toLocaleString()}</p>
            </div>
            <div>
              <div className="flex items-center gap-2 text-fg-subtle mb-1">
                <Clock className="w-4 h-4" />
                <span className="text-xs font-medium">Delivery</span>
              </div>
              <p className="font-bold text-fg">{proposal.delivery_days} Days</p>
            </div>
            <div>
              <div className="flex items-center gap-2 text-fg-subtle mb-1">
                <Calendar className="w-4 h-4" />
                <span className="text-xs font-medium">Submitted</span>
              </div>
              <p className="font-bold text-fg">{new Date(proposal.created_at).toLocaleDateString()}</p>
            </div>
          </div>

          <div>
            <h4 className="text-sm font-bold text-fg mb-3">Cover Letter</h4>
            <div className="bg-surface border border-line-soft rounded-lg p-4 text-fg-muted leading-relaxed whitespace-pre-wrap">
              {proposal.cover_letter}
            </div>
          </div>
        </div>

        <div className="border-t px-6 py-4 bg-surface-2 flex justify-end">
          <button
            onClick={onClose}
            className="px-6 py-2 bg-brand text-brand-fg rounded-lg font-medium hover:bg-inverse transition-colors"
          >
            Close
          </button>
        </div>
    </Dialog>
  );
}
