import React from "react";
import { ShieldCheck, Info, Mail, Edit, Lock, AlertCircle } from "lucide-react";
import { useModal } from "../../../hooks/modal/useModalStore";
import { usePaymentSettings } from "./profileQueries";

export default function PaymentSettings() {
  const { data: settings, isLoading } = usePaymentSettings();
  const { openModal } = useModal();

  if (isLoading) return <div className="p-10 text-center">Loading...</div>;

  return (
    <div className="max-w-4xl mx-auto py-10 px-4">
      <div className="mb-8">
        <h1 className="text-2xl font-bold text-fg">Payment Methods</h1>
        <p className="text-fg-subtle text-sm mt-1">Configure how you receive your earnings.</p>
      </div>
      
      <div className="bg-surface rounded-2xl border border-line shadow-sm overflow-hidden">
        <div className="p-8">
          <div className="flex justify-between items-start mb-8">
            <div className="flex gap-4">
              <div className="w-12 h-12 bg-brand-soft rounded-full flex items-center justify-center text-brand">
                <Mail size={24} />
              </div>
              <div>
                <h3 className="text-lg font-bold text-fg">PayPal Account</h3>
                <p className="text-fg-subtle text-sm">Funds are released to this email address.</p>
              </div>
            </div>
            <button 
              onClick={() => openModal('edit-payment', { currentEmail: settings?.paypal_email })}
              className="flex items-center gap-2 px-4 py-2 bg-surface-2 text-brand rounded-lg text-sm font-semibold hover:bg-brand-hover-soft transition"
            >
              <Edit size={16} /> Edit
            </button>
          </div>

          <div className="bg-surface-2 rounded-xl p-6 border border-line-soft flex items-center justify-between">
            <div>
              <span className="text-[10px] font-bold text-fg-subtle uppercase tracking-widest">Active Email</span>
              <p className="text-lg font-medium text-fg">{settings?.paypal_email}</p>
            </div>
            {/* {settings?.is_verified ? (
              <div className="flex items-center gap-1 text-success bg-success-soft px-3 py-1 rounded-full text-xs font-bold border border-success/30">
                <ShieldCheck size={14} /> Verified
              </div>
            ) : (
              <div className="flex items-center gap-1 text-warning bg-warning-soft px-3 py-1 rounded-full text-xs font-bold border border-warning/30">
                <AlertCircle size={14} /> Pending Verification
              </div>
            )} */}
          </div>

          <div className="mt-8 p-4 bg-brand-soft rounded-xl border border-brand-line flex gap-3">
            <Info className="text-brand shrink-0" size={20} />
            <div className="text-sm text-brand leading-relaxed">
              <p className="font-bold mb-1">How Payouts Work:</p>
              When a client approves your work, the payment moves from <strong>Escrow</strong> to your <strong>Pending Balance</strong>. After a short security period, it is automatically transferred to your PayPal account.
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}