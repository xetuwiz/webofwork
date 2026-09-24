import { useState } from "react";
import { useDisputeMutation } from "../../../freelancer/contracts/contractMutation.js";

export default function ClientDisputeModal({ onClose, contractId }) {
  const [reason, setReason] = useState("");
  const [description, setDescription] = useState("");
  const mutation = useDisputeMutation();
  return <div className="modal-backdrop" role="presentation"><form className="modal-card form-card" onSubmit={(e) => { e.preventDefault(); mutation.mutate({ contractId, reason, description }, { onSuccess: onClose }); }}><h2>Raise a dispute</h2><label>Reason<input required value={reason} onChange={(e) => setReason(e.target.value)} /></label><label>Description<textarea required value={description} onChange={(e) => setDescription(e.target.value)} /></label><div className="button-row"><button type="button" className="button button--quiet" onClick={onClose}>Cancel</button><button className="button button--accent" disabled={mutation.isPending}>Submit dispute</button></div></form></div>;
}
