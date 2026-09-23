import { useReleasePayout } from "./payoutMutations.js";

export default function PayoutReleaseModal({ payment, onClose }) {
  const mutation = useReleasePayout();
  if (!payment) return null;
  const confirm = () => mutation.mutate(payment.id, { onSuccess: onClose });
  return <div className="admin-modal-backdrop" role="presentation" onMouseDown={onClose}><section className="admin-modal" role="dialog" aria-modal="true" aria-labelledby="payout-title" onMouseDown={(event) => event.stopPropagation()}><button className="admin-modal__close" type="button" onClick={onClose} aria-label="Close">X</button><span className="app-shell__eyebrow">Payout approval</span><h2 id="payout-title">Release payout?</h2><p>Release {new Intl.NumberFormat("en-US", { style: "currency", currency: "USD" }).format(Number(payment.amount ?? 0))} for payment #{payment.id}.</p>{mutation.isError && <div className="admin-form-error">The payout could not be released.</div>}<div className="admin-modal__actions"><button className="button button--quiet" type="button" onClick={onClose}>Cancel</button><button className="button button--accent" type="button" disabled={mutation.isPending} onClick={confirm}>{mutation.isPending ? "Releasing..." : "Release payout"}</button></div></section></div>;
}