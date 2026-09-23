import { Link } from "react-router-dom";

export default function JoinNow() {
  return <section className="cta-section"><div><span className="app-shell__eyebrow">The next move is yours</span><h2>Start with a better brief.</h2></div><Link className="button button--accent" to="/create-project">Post your project</Link></section>;
}