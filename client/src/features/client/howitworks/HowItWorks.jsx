import { Link } from "react-router-dom";
import PublicPageFrame from "../../../components/common/PublicPageFrame.jsx";

const steps = [["01", "Post your project", "Describe the outcome, constraints, and budget."], ["02", "Meet relevant talent", "Compare experience and proposals in one workspace."], ["03", "Work in milestones", "Keep delivery, feedback, and approvals visible."], ["04", "Release with confidence", "Approve the finished work against the agreement."]];

export default function HowItWorks() {
  return <PublicPageFrame><section className="how-hero"><div><span className="app-shell__eyebrow">How it works</span><h1>A simple process with enough structure to protect the work.</h1><p>WebOfWork gives both sides a shared sequence: brief, match, deliver, review.</p><Link className="button button--accent" to="/find-talents">Find talent</Link></div><div className="how-hero__visual"><div className="how-hero__image" /><span>Brief / Match / Deliver</span></div></section><section className="content-section"><div className="section-heading"><span className="app-shell__eyebrow">The workflow</span><h2>Four steps. One shared record.</h2></div><div className="workflow-list">{steps.map(([number, title, description]) => <article key={number}><span>{number}</span><div><h3>{title}</h3><p>{description}</p></div></article>)}</div></section></PublicPageFrame>;
}