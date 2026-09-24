import { useState } from "react";

export default function ApproveContractModal({ onClose, onApprove }) {
  const [agreed, setAgreed] = useState(false);
  return <div className="modal-backdrop" role="presentation"><section className="modal-card form-card"><h2>Approve deliverable</h2><p>Confirm that the submitted deliverable is complete and acceptable.</p><label><input type="checkbox" checked={agreed} onChange={(e) => setAgreed(e.target.checked)} /> I reviewed the deliverable</label><div className="button-row"><button className="button button--quiet" onClick={onClose}>Cancel</button><button className="button button--accent" disabled={!agreed} onClick={() => { onApprove(); onClose(); }}>Approve</button></div></section></div>;
}
