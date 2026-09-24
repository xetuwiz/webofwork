import { useState } from "react";
import { useContract } from "./contractQueries";
import { useAcceptContract, useRequestRevision, useUpdateContractStatus } from "./contractMutations";
import { useParams } from "react-router-dom";
import { useModal } from "../../../hooks/modal/useModalStore.jsx";
import ContractOfferModal from "./components/ContractOfferModal.jsx";
import formatMoney from "../../../utils/formatMoney";

export default function ContractDetail() {
  const { id } = useParams();
  const { data: contract, isLoading, isError } = useContract(id);
  const [reason, setReason] = useState("");
  const accept = useAcceptContract();
  const revision = useRequestRevision();
  const status = useUpdateContractStatus();
  const { openModal } = useModal();
  if (isLoading) return <section className="content-section"><p>Loading contract…</p></section>;
  if (isError || !contract) return <section className="content-section"><p role="alert">Unable to load this contract.</p></section>;
  const value = (a, b) => contract[a] ?? contract[b];
  return <section className="content-section"><span className="app-shell__eyebrow">Contract #{id}</span><h1>{value("projectTitle", "project_title") || "Contract detail"}</h1><p>{value("freelancerName", "freelancer_name")} · {value("status", "status")}</p><p>{formatMoney(value("totalAmount", "total_amount"))}</p><div className="card-list">{(value("deliverables", "deliverables") || []).map((item) => <article className="card" key={item.id}><strong>{item.title}</strong><p>{item.notes || "No notes"}</p></article>)}</div><div className="button-row">{value("status", "status") === "Offered" && <button className="button button--accent" onClick={() => openModal("contract-offer", { contract })}>Accept contract</button>}{value("status", "status") === "Submitted" && <button className="button button--accent" onClick={() => openModal("approve-contract", { onApprove: () => status.mutate({ contractId: id, status: "Completed" }) })}>Approve work</button>}{value("status", "status") === "Active" && <button className="button button--quiet" onClick={() => openModal("client-dispute", { contractId: id })}>Raise dispute</button>}{value("status", "status") === "Active" && <form className="form-card" onSubmit={(e) => { e.preventDefault(); revision.mutate({ contractId: id, reason }); }}><h2>Request a revision</h2><input required value={reason} onChange={(e) => setReason(e.target.value)} placeholder="Explain what needs changing" /><button className="button button--secondary" disabled={revision.isPending}>Request revision</button></form>}</div></section>;
}