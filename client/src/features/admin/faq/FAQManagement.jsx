import { useState } from "react";
import { useAdminFAQs } from "./faqQueries.js";
import { useFAQMutations } from "./faqMutations.js";

export default function FAQManagement() {
  const { data, isLoading, isError } = useAdminFAQs();
  const { create, update, remove } = useFAQMutations();
  const [form, setForm] = useState({ question: "", answer: "", category: "General", sortOrder: 0 });
  const rows = data?.items || data?.results || (Array.isArray(data) ? data : []);
  const submit = (e) => { e.preventDefault(); create.mutate(form, { onSuccess: () => setForm({ question: "", answer: "", category: "General", sortOrder: 0 }) }); };
  return <section className="admin-subsection"><h1>FAQ management</h1>{isLoading && <div className="admin-loading">Loading FAQ entries…</div>}{isError && <div className="admin-form-error">Unable to load FAQ entries.</div>}<form className="form-card" onSubmit={submit}><h2>Add FAQ</h2><input required placeholder="Question" value={form.question} onChange={(e) => setForm({ ...form, question: e.target.value })} /><textarea required placeholder="Answer" value={form.answer} onChange={(e) => setForm({ ...form, answer: e.target.value })} /><select value={form.category} onChange={(e) => setForm({ ...form, category: e.target.value })}>{["General", "Account", "Projects", "Payments"].map((item) => <option key={item}>{item}</option>)}</select><button className="button button--accent" disabled={create.isPending}>Add FAQ</button></form><div className="admin-payout-list">{rows.map((row) => <article className="admin-payout-row" key={row.id}><div><strong>{row.question}</strong><small>{row.answer}</small></div><button className="button button--quiet" onClick={() => remove.mutate(row.id)} disabled={remove.isPending}>Delete</button></article>)}</div></section>;
}
