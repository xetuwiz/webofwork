import { useState } from "react";
import { useAcceptContract } from "../contractMutations";

export default function ContractOfferModal({ onClose, contract }) {
  const [signature, setSignature] = useState("");
  const mutation = useAcceptContract();
  return <div className="modal-backdrop" role="presentation"><form className="modal-card form-card" onSubmit={(e) => { e.preventDefault(); mutation.mutate({ contractId: contract.id, signature, signatureType: "text" }, { onSuccess: onClose }); }}><h2>Review and accept contract</h2><p>Amount: {contract.totalAmount ?? contract.total_amount} USD</p><label>Client signature<input required value={signature} onChange={(e) => setSignature(e.target.value)} /></label><div className="button-row"><button type="button" className="button button--quiet" onClick={onClose}>Cancel</button><button className="button button--accent" disabled={mutation.isPending}>Accept contract</button></div></form></div>;
}
