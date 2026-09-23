export default function RoutePlaceholder({ area, title, description }) {
  return (
    <section className="route-placeholder" aria-labelledby="route-placeholder-title">
      <span className="app-shell__eyebrow">{area}</span>
      <h1 id="route-placeholder-title">{title}</h1>
      <p>{description}</p>
    </section>
  );
}