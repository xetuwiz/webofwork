import React from 'react';
import { X, DollarSign, Loader2, AlertCircle } from 'lucide-react';
import { useReleasePayout } from './payoutMutations';
import Dialog from "../../../components/common/Dialog";
import Button from "../../../components/common/Button";

export default function PayoutReleaseModal({ contract, isOpen, onClose }) {
  const { mutate: release, isPending } = useReleasePayout();

  if (!isOpen) return null;

  const handleConfirm = () => {
    release(contract.id, { 
      onSuccess: () => onClose() 
    });
  };

  return (
    <Dialog
      open={isOpen}
      onClose={onClose}
      title="Confirm Payment Release"
      size="md"
      className="overflow-hidden"
    >
        <div className="flex items-center justify-between border-b p-4">
          <h3 className="text-lg font-bold text-fg">Confirm Payment Release</h3>
          <button onClick={onClose} className="text-fg-subtle hover:text-fg-muted transition-colors">
            <X size={20} />
          </button>
        </div>

        <div className="p-6">
          <div className="mb-6 flex flex-col items-center text-center">
            <div className="w-16 h-16 bg-success-soft text-success rounded-full flex items-center justify-center mb-4">
              <DollarSign size={32} />
            </div>
            <h4 className="text-xl font-bold text-fg mb-1">
              ${Number(contract.total_amount || 0).toLocaleString()}
            </h4>
            {/* Not "To be sent to the freelancer". `total_amount` here is the
                contracted share, and the payout is computed after refunds with
                the commission pro-rated onto what is left -- so on a partially
                refunded payment the amount actually sent is lower. Labelling this
                as the payout figure would have the admin confirming one number and
                the system paying another.

                The exact net is not recomputed here on purpose: duplicating the
                pro-rata formula in the client is how it drifts out of step with
                `ReleaseCoreAsync`. The server sends `refunded_amount` so the
                discrepancy is visible rather than implied. */}
            <p className="text-sm text-fg-subtle">Freelancer's contracted share</p>
            {contract.platform_fee > 0 && (
              <p className="text-xs text-fg-subtle mt-1">
                ${Number(contract.platform_fee).toLocaleString()} platform commission is retained
              </p>
            )}
            {Number(contract.refunded_amount || 0) > 0 && (
              <p className="text-xs text-warning-fg mt-1">
                ${Number(contract.refunded_amount).toLocaleString()} was already refunded,
                so less than this will be paid out
              </p>
            )}
          </div>

          <div className="space-y-4 rounded-xl bg-surface-2 p-4 border border-line-soft">
            <div className="flex items-start gap-3">
              <AlertCircle className="text-brand shrink-0 mt-0.5" size={16} />
              <p className="text-xs text-fg-muted leading-relaxed">
                {/* Corrected. This said the funds are "split between the
                      freelancer's registered PayPal and your platform's master
                      PayPal account". They are not, and cannot be:
                      `PayBothPartiesAsync` deliberately sends a one-item batch,
                      because paying `GlobalSettings.PayPalEmail` would ask PayPal
                      to pay the sending account itself, which it rejects with
                      "Payment to self is not allowed" -- failing the whole batch
                      and with it the freelancer's payout. The commission stays in
                      the ledger and in this platform account's balance. */}
                  By confirming, the freelancer's share is sent to their registered
                  PayPal account. The platform commission is not paid out separately
                  &mdash; it was collected on the client's payment and remains in this
                  platform account's balance.
              </p>
            </div>
          </div>

          <div className="mt-8 flex gap-3">
            <button
              type="button"
              onClick={onClose}
              disabled={isPending}
              className="flex-1 rounded-lg border border-line-strong py-3 text-sm font-semibold text-fg-muted hover:bg-surface-2 transition-colors disabled:opacity-50"
            >
              Cancel
            </button>
            <Button
              onClick={handleConfirm}
              variant="primary"
              size="none"
              loading={isPending}
              className="flex-1 py-3 text-sm active:scale-[0.98]"
            >
              <DollarSign size={18} />
              Release Funds
            </Button>
          </div>
          
          <p className="mt-4 text-[10px] text-center text-fg-subtle uppercase tracking-widest font-bold">
            This action cannot be undone
          </p>
        </div>
    </Dialog>
  );
}
