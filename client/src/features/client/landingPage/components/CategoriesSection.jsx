const categories = ["Product design", "Software development", "Marketing", "Writing and content", "Operations", "Business strategy"];

export default function CategoriesSection() {
  return (
    <section className="content-section categories-section">
      <div className="section-heading section-heading--split"><div><span className="app-shell__eyebrow">Find the right context</span><h2>Work across the categories that move businesses forward.</h2></div><p>Specialists bring the depth. Clients bring the ambition. The platform gives both sides a better starting point.</p></div>
      <div className="category-list">{categories.map((category, index) => <div className="category-item" key={category}><span>0{index + 1}</span><strong>{category}</strong><span aria-hidden="true">↗</span></div>)}</div>
    </section>
  );
}