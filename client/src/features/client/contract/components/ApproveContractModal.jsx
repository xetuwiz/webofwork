import React, { useState } from "react";
import { X, AlertTriangle, CheckCircle2 } from "lucide-react";
import { useModal } from "../../../../hooks/modal/useModalStore";
import Dialog from "../../../../components/common/Dialog";

export default function ApproveContractModal() {
  const { closeModal, modalProps } = useModal();
  const [isAgreed, setIsAgreed] = useState(false);
  
  const { onApprove, projectName, freelancerName, amount } = modalProps;

  const handleFinalApproval = () => {
    if (isAgreed) {
      onApprove();
      closeModal();
    }
  };

  return (
    <Dialog
            open
            onClose={closeModal}
      title="Approve Deliverables"
      size="md"
      className="overflow-hidden"
    >
        
        <div className="flex items-center justify-between px-6 py-4 border-b border-line-soft">
          <h3 className="text-lg font-bold text-fg">Approve Deliverables</h3>
          <button onClick={closeModal} className="p-1 hover:bg-surface-3 rounded-full transition-colors">
            <X className="w-5 h-5 text-fg-subtle" />
          </button>
        </div>

        <div className="p-6 space-y-6">
          <div className="flex items-center gap-4 p-4 bg-warning-soft rounded-xl border border-warning/30">
            <div className="w-10 h-10 flex-shrink-0 bg-warning-soft rounded-full flex items-center justify-center">
              <AlertTriangle className="w-6 h-6 text-warning" />
            </div>
            <div>
              <p className="text-sm font-semibold text-warning-soft-fg">Important Notice</p>
              <p className="text-xs text-warning leading-relaxed">
                Approving this project signals that all work is completed to your satisfaction.
              </p>
            </div>
          </div>

          <div className="space-y-3">
            <h4 className="text-xs font-bold text-fg-subtle uppercase tracking-wider">Completion Instructions</h4>
            <ul className="space-y-3">
              <li className="flex gap-3 text-xs text-fg-muted leading-5">
                <CheckCircle2 className="w-4 h-4 text-emerald-500 flex-shrink-0" />
                <span>The payment of <strong>${amount}</strong> will be moved from Escrow to the "Ready for Payout" stage.</span>
              </li>
              <li className="flex gap-3 text-xs text-fg-muted leading-5">
                <CheckCircle2 className="w-4 h-4 text-emerald-500 flex-shrink-0" />
                {/* The `**24 hours**` here was literal Markdown inside JSX, so it
                    rendered as visible asterisks, and the number was wrong anyway:
                    ContractService sets `PayoutEligibleAfter = CompletedAt.AddDays(7)`,
                    so the real window is 7 days, not 24 hours. This copy is shown
                    to a client at the moment they approve, so it has to match the
                    rule that actually holds their money. */}
                <span>You have <strong>7 days</strong> from this moment to report any technical issues or missing source files.</span>
              </li>
              <li className="flex gap-3 text-xs text-fg-muted leading-5">
                <CheckCircle2 className="w-4 h-4 text-emerald-500 flex-shrink-0" />
                <span>Once the 7-day window expires, the platform admin will release the funds to {freelancerName}&apos;s PayPal account.</span>
              </li>
            </ul>
          </div>

          <div className="p-4 bg-surface-2 rounded-xl border border-line">
            <label className="flex gap-3 cursor-pointer group">
              <input
                type="checkbox"
                checked={isAgreed}
                onChange={(e) => setIsAgreed(e.target.checked)}
                className="w-5 h-5 mt-0.5 rounded border-line-strong text-brand focus-visible:ring-brand transition-all cursor-pointer"
              />
              <span className="text-xs font-medium text-fg-muted leading-5 group-hover:text-fg transition-colors">
                I have reviewed all deliverables for <strong>{projectName}</strong> and I accept the terms of completion.
              </span>
            </label>
          </div>
        </div>

        <div className="px-6 py-4 bg-surface-2 border-t border-line-soft flex gap-3">
          <button 
            onClick={closeModal}
            className="flex-1 px-4 py-2.5 text-sm font-semibold text-fg-muted hover:bg-surface-inset rounded-xl transition-colors"
          >
            Go Back
          </button>
          <button 
            onClick={handleFinalApproval}
            disabled={!isAgreed}
            className={`flex-1 px-4 py-2.5 text-sm font-bold text-white rounded-xl shadow-lg shadow-blue-200 transition-all ${
              isAgreed ? 'bg-brand hover:bg-brand-hover' : 'bg-surface-inset cursor-not-allowed'
            }`}
          >
            Confirm Approval
          </button>
        </div>
    </Dialog>
  );
}
