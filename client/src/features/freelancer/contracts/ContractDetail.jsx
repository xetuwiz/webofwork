import { useState } from "react";
import { useContractDetail } from "./contractsQueries";
import { useSubmitWork, useRevisionAction, useDisputeMutation } from "./contractMutation";
import { useParams } from "react-router-dom";
import formatMoney from "../../../utils/formatMoney";

export default function ContractDetail() {
  const { id } = useParams();
  const { data: contract, isLoading, isError } = useContractDetail(id);
  const submit = useSubmitWork();
  const revision = useRevisionAction();
  const dispute = useDisputeMutation();
  const [title, setTitle] = useState("");
  const [notes, setNotes] = useState("");
  const [linkUrl, setLinkUrl] = useState("");
  if (isLoading) return <section className="content-section"><p>Loading contract…</p></section>;
  if (isError || !contract) return <section className="content-section"><p role="alert">Unable to load this contract.</p></section>;
  const value = (a, b) => contract[a] ?? contract[b];
  return <section className="content-section"><span className="app-shell__eyebrow">Contract #{id}</span><h1>{value("projectTitle", "project_title") || "Contract detail"}</h1><p>{value("clientName", "client_name")} · {value("status", "status")}</p><p>{formatMoney(value("totalAmount", "total_amount"))}</p><div className="card-list">{(value("deliverables", "deliverables") || []).map((item) => <article className="card" key={item.id}><strong>{item.title}</strong><p>{item.deliverableType || item.deliverable_type || "file"}</p><a href={item.linkUrl || item.link_url} target="_blank" rel="noreferrer">Open deliverable</a></article>)}</div>{contract.status === "Active" && <form className="form-card" onSubmit={(event) => { event.preventDefault(); submit.mutate({ id, payload: { title, notes, linkUrl, deliverableType: linkUrl ? "link" : "file" } }); }}><h2>Submit single deliverable</h2><input required value={title} onChange={(e) => setTitle(e.target.value)} placeholder="Deliverable title" /><input value={linkUrl} onChange={(e) => setLinkUrl(e.target.value)} placeholder="https://…" /><textarea value={notes} onChange={(e) => setNotes(e.target.value)} placeholder="Notes" /><button className="button button--accent" disabled={submit.isPending}>Submit work</button></form>}{(value("revisions", "revisions") || []).map((item) => <button key={item.id} className="button button--quiet" onClick={() => revision.mutate({ revisionId: item.id, action: "accept" })}>Accept revision</button>)}{["Active", "Submitted", "Disputed"].includes(contract.status) && <button className="button button--quiet" onClick={() => dispute.mutate({ contractId: id, reason: "Scope disagreement", description: "Raised from the contract workspace." })}>Raise dispute</button>}</section>;
}