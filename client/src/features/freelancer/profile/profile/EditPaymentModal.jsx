import React, { useState } from "react";
import { X, Loader2 } from "lucide-react";
import { useUpdatePaymentSettings } from "./profileMutation";
import Dialog from "../../../components/common/Dialog";
import Button from "../../../components/common/Button";
import { Field } from "../../../components/common/Field";

export default function EditPaymentModal({ isOpen, onClose, currentEmail }) {
  const [email, setEmail] = useState(currentEmail || "");
  const { mutate, isLoading } = useUpdatePaymentSettings();

  if (!isOpen) return null;

  const handleSubmit = (e) => {
    e.preventDefault();
    mutate({ paypal_email: email }, {
      onSuccess: onClose()
    });
  };

  return (
    <Dialog
      open={isOpen}
      onClose={onClose}
      title="Update PayPal Email"
      size="md"
      className="overflow-hidden"
    >
        <div className="flex justify-between items-center px-6 py-4 border-b">
          <h3 className="font-bold text-fg">Update PayPal Email</h3>
          <button onClick={onClose} className="p-1 hover:bg-surface-3 rounded-full transition">
            <X size={20} />
          </button>
        </div>
        
        <form onSubmit={handleSubmit} className="p-6">
          <div className="space-y-4">
            <div>
              <Field label="PayPal Email Address">
                {(f) => (
                  <input
                    type="email"
                    required
                    autoFocus
                    value={email}
                    onChange={(e) => setEmail(e.target.value)}
                    {...f}
                    className="w-full px-4 py-2 border border-line-strong rounded-lg focus-visible:ring-2 focus-visible:ring-brand outline-none"
                    placeholder="yourname@example.com"
                  />
                )}
              </Field>
            </div>
          </div>

          <div className="mt-8 flex gap-3">
            <button type="button" onClick={onClose} className="flex-1 px-4 py-2 text-fg-muted font-medium">
              Cancel
            </button>
                        <Button
              type="submit"
              variant="primary"
              size="none"
              loading={isLoading}
              className="flex-1 px-4 py-2 font-medium"
            >
              Update
            </Button>
          </div>
        </form>
    </Dialog>
  );
}
