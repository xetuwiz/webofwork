const steps = [
  ["01", "Describe the work", "Turn an idea into a focused brief with the context professionals need."],
  ["02", "Choose with confidence", "Compare relevant profiles, proposals, and practical fit in one place."],
  ["03", "Deliver in milestones", "Keep decisions, files, feedback, and payment tied to the agreed work."],
];

export default function StepsSection() {
  return (
    <section className="content-section content-section--lined">
      <div className="section-heading"><span className="app-shell__eyebrow">The working rhythm</span><h2>From a loose idea to a finished result.</h2></div>
      <div className="steps-grid">{steps.map(([number, title, description]) => <article className="step-item" key={number}><span>{number}</span><h3>{title}</h3><p>{description}</p></article>)}</div>
    </section>
  );
}