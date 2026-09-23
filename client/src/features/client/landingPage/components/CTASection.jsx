import { Link } from "react-router-dom";

export default function CTASection() {
  return <section className="cta-section"><div><span className="app-shell__eyebrow">Make the next project easier</span><h2>Bring the work into focus.</h2></div><Link className="button button--accent" to="/create-project">Start a project</Link></section>;
}