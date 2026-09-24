import { useState } from "react";
import { useAcceptOffer } from "./offersMutations";

export default function OfferModal({ onClose, offer }) {
  const [signature, setSignature] = useState("");
  const [agreed, setAgreed] = useState(false);
  const mutation = useAcceptOffer();
  return <div className="modal-backdrop" role="presentation"><form className="modal-card form-card" onSubmit={(e) => { e.preventDefault(); mutation.mutate({ contractId: offer.id, data: { signature, signatureType: "text" } }, { onSuccess: onClose }); }}><h2>Review and sign offer</h2><p>{offer.projectTitle || offer.project_title} · {offer.totalAmount || offer.total_amount} USD</p><label>Digital signature<input required value={signature} onChange={(e) => setSignature(e.target.value)} /></label><label><input type="checkbox" checked={agreed} onChange={(e) => setAgreed(e.target.checked)} /> I accept the contract terms</label><div className="button-row"><button type="button" className="button button--quiet" onClick={onClose}>Cancel</button><button className="button button--accent" disabled={!agreed || mutation.isPending}>Sign and start</button></div></form></div>;
}
