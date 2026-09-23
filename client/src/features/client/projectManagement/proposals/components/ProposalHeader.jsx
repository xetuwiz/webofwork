import { Link, useParams } from "react-router-dom";

export default function ProposalHeader({ title, budget, proposalCount, postedTime }) {
  const { id } = useParams();
  return <header className="proposal-header"><div><span className="app-shell__eyebrow">Project proposals</span><h2>{title}</h2><div className="proposal-header__meta"><span>Budget: {budget}</span><span>Proposals: {proposalCount}</span><span>{postedTime}</span></div></div><Link className="button button--quiet" to={`/my-projects/${id}`}>View project</Link></header>;
}