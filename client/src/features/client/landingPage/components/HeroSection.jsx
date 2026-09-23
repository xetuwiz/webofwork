import { Link } from "react-router-dom";

export default function HeroSection() {
  return (
    <section className="landing-hero">
      <div className="landing-hero__copy">
        <span className="app-shell__eyebrow">A clearer way to get work done</span>
        <h1>Good work starts with the right working relationship.</h1>
        <p>WebOfWork brings clients and independent professionals together with clear briefs, accountable delivery, and protected payments.</p>
        <div className="landing-hero__actions">
          <Link className="button button--accent" to="/create-project">Post a project</Link>
          <Link className="button button--quiet" to="/find-talents">Explore talent</Link>
        </div>
      </div>
      <div className="landing-hero__visual" role="img" aria-label="A collaborative workspace represented by layered work surfaces">
        <div className="landing-hero__image" />
        <div className="landing-hero__note"><span>Brief clarity</span><strong>Before the first message</strong></div>
        <div className="landing-hero__status"><i /> Protected milestone flow</div>
      </div>
    </section>
  );
}