import { useState } from "react";
import { useVerifyPayment } from "../contractMutations.js";
import { validatePaymentForm } from "../paymentForm.js";

export default function ContractOfferModal({ onClose, contract }) {
  const [orderId, setOrderId] = useState("");
  const [amount, setAmount] = useState(contract.totalAmount ?? contract.total_amount ?? "");
  const verify = useVerifyPayment();
  const submit = (event) => { event.preventDefault(); const values = { paypalOrderId: orderId, paypalAmountUsd: Number(amount) }; if (!validatePaymentForm(values).valid) return; verify.mutate({ contractId: contract.id, ...values }, { onSuccess: onClose }); };
  return <div className="modal-backdrop" role="presentation"><form className="modal-card form-card" onSubmit={submit}><h2>Fund contract with PayPal</h2><label>PayPal order ID<input required value={orderId} onChange={(event) => setOrderId(event.target.value)} /></label><label>USD amount<input required type="number" min="0.01" step="0.01" value={amount} onChange={(event) => setAmount(event.target.value)} /></label><div className="button-row"><button type="button" className="button button--quiet" onClick={onClose}>Cancel</button><button className="button button--accent" disabled={verify.isPending}>{verify.isPending ? "Verifying..." : "Verify payment"}</button></div></form></div>;
}
