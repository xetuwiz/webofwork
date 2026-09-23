import { Link, useNavigate } from "react-router-dom";

export default function NotFound() {
  const navigate = useNavigate();

  return (
    <section className="not-found" aria-labelledby="not-found-title">
      <span className="not-found__code">404</span>
      <span className="app-shell__eyebrow">Route not found</span>
      <h1 id="not-found-title">This address is not part of the workspace.</h1>
      <p>Use the workspace navigation or return to the WebOfWork home.</p>
      <div className="not-found__actions">
        <button type="button" className="button button--quiet" onClick={() => navigate(-1)}>Go back</button>
        <Link className="button button--accent" to="/">Return home</Link>
      </div>
    </section>
  );
}