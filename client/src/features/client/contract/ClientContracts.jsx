import { useState } from "react";
import { useMyContracts } from "../../freelancer/contracts/contractsQueries.js";
import { useNavigate } from "react-router-dom";
import formatMoney from "../../../utils/formatMoney";

export default function ClientContracts() {
  const navigate = useNavigate();
  const [search, setSearch] = useState("");
  const { data, isLoading, isError } = useMyContracts({ role: "client", search, page: 1, pageSize: 20 });
  const contracts = data?.results || (Array.isArray(data) ? data : []);
  return <section className="content-section"><span className="app-shell__eyebrow">Client workspace</span><h1>Contracts</h1><input className="input" value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Search contracts" />{isLoading && <p>Loading contracts…</p>}{isError && <p role="alert">Unable to load contracts.</p>}{!isLoading && !isError && contracts.length === 0 && <p>No contracts found.</p>}<div className="card-list">{contracts.map((contract) => <article className="card" key={contract.id}><div><strong>{contract.projectTitle || contract.project_title || `Contract #${contract.id}`}</strong><p>{contract.freelancerName || contract.freelancer_name || "Freelancer"}</p></div><div><strong>{formatMoney(contract.totalAmount ?? contract.total_amount)}</strong><p>{contract.status}</p></div><button className="button button--secondary" onClick={() => navigate(`/contracts/${contract.id}`)}>Open</button></article>)}</div></section>;
}