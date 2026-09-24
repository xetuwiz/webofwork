const features = [
  ["Briefs with signal", "Put goals, constraints, and expectations in the same room before work begins."],
  ["Delivery with accountability", "Make progress visible and decisions easy to revisit."],
  ["Support when the work gets complicated", "A documented path for questions, revisions, and resolution."],
];

export default function Features() {
  return <section className="content-section feature-band"><div className="section-heading"><span className="app-shell__eyebrow">Less noise, more momentum</span><h2>Infrastructure that respects the work.</h2></div><div className="feature-grid">{features.map(([title, description]) => <article key={title}><span className="feature-grid__line" /><h3>{title}</h3><p>{description}</p></article>)}</div></section>;
}