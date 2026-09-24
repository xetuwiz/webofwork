import { useState } from "react";
import { useMyContracts } from "./contractsQueries";
import { useNavigate } from "react-router-dom";
import formatMoney from "../../../utils/formatMoney";

const statuses = ["All", "Offered", "Active", "Submitted", "Completed", "Disputed", "Cancelled"];
export default function Contracts() {
  const navigate = useNavigate();
  const [status, setStatus] = useState("All");
  const { data, isLoading, isError } = useMyContracts({ role: "freelancer", status: status === "All" ? undefined : status, page: 1, pageSize: 20 });
  const contracts = data?.results || (Array.isArray(data) ? data : []);
  return <section className="content-section"><span className="app-shell__eyebrow">Freelancer workspace</span><h1>My contracts</h1><p>Review signed work, delivery status, and payment readiness.</p><div className="button-row">{statuses.map((item) => <button key={item} className={`button ${status === item ? "button--accent" : "button--quiet"}`} onClick={() => setStatus(item)}>{item}</button>)}</div>{isLoading && <p>Loading contracts…</p>}{isError && <p role="alert">Unable to load contracts.</p>}{!isLoading && !isError && contracts.length === 0 && <p>No contracts found.</p>}<div className="card-list">{contracts.map((contract) => <article className="card" key={contract.id}><div><strong>{contract.projectTitle || contract.project_title || `Contract #${contract.id}`}</strong><p>{contract.freelancerName || contract.freelancer_name || "Freelancer"}</p></div><div><strong>{formatMoney(contract.totalAmount ?? contract.total_amount)}</strong><p>{contract.status}</p></div><button className="button button--secondary" onClick={() => navigate(`/freelancer/contracts/${contract.id}`)}>Open</button></article>)}</div></section>;
}