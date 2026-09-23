import { Link } from "react-router-dom";

export default function ClientHeroSection() {
  return <section className="client-home-hero"><div><span className="app-shell__eyebrow">Your work, in focus</span><h1>Find the people who make the next step possible.</h1><p>Post a brief, compare the right experience, and move from conversation to delivery with less friction.</p><Link className="button button--accent" to="/create-project">Create a project</Link></div><div className="client-home-hero__panel"><span>Active workspace</span><strong>Brief → Match → Deliver</strong><small>One shared path for every project.</small></div></section>;
}