import { useState } from "react";
import { useTermsSections } from "./termsQueries.js";
import { useTermsMutations } from "./temsMutations.js";

export default function TermAndConditions() {
  const { data, isLoading, isError } = useTermsSections();
  const { create, update, remove } = useTermsMutations();
  const [form, setForm] = useState({ heading: "", description: "", sortOrder: 0 });
  const rows = data?.items || data?.results || (Array.isArray(data) ? data : []);
  const submit = (e) => { e.preventDefault(); create.mutate(form, { onSuccess: () => setForm({ heading: "", description: "", sortOrder: 0 }) }); };
  return <section className="admin-subsection"><h1>Terms management</h1>{isLoading && <div className="admin-loading">Loading terms…</div>}{isError && <div className="admin-form-error">Unable to load terms.</div>}<form className="form-card" onSubmit={submit}><h2>Add terms section</h2><input required placeholder="Heading" value={form.heading} onChange={(e) => setForm({ ...form, heading: e.target.value })} /><textarea required placeholder="Description" value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} /><button className="button button--accent" disabled={create.isPending}>Add section</button></form><div className="admin-payout-list">{rows.map((row) => <article className="admin-payout-row" key={row.id}><div><strong>{row.heading}</strong><small>{row.description}</small></div><button className="button button--quiet" onClick={() => remove.mutate(row.id)}>Delete</button></article>)}</div></section>;
}
