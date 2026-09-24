import { useState } from "react";
import { useGlobalSettings } from "./adminSettingsQueries.js";
import { useUpdateSettings } from "./adminsettingsMutations.js";

export default function AdminSettings() {
  const { data, isLoading, isError } = useGlobalSettings();
  const update = useUpdateSettings();
  const [form, setForm] = useState({ SupportEmail: "", CommissionPercentage: 0, PayPalEmail: "", MinBriefConfidence: 0, ProposalScreeningThreshold: 0, MatchScoreThreshold: 0, DefaultCurrency: "USD" });
  const set = (key, value) => setForm((current) => ({ ...current, [key]: value }));
  const load = () => data && setForm({ SupportEmail: data.supportEmail ?? "", CommissionPercentage: data.commissionPercentage ?? 0, PayPalEmail: data.payPalEmail ?? "", MinBriefConfidence: data.minBriefConfidence ?? 0, ProposalScreeningThreshold: data.proposalScreeningThreshold ?? 0, MatchScoreThreshold: data.matchScoreThreshold ?? 0, DefaultCurrency: data.defaultCurrency ?? "USD" });
  return <section className="admin-subsection"><h1>Platform settings</h1>{isLoading && <div className="admin-loading">Loading settings…</div>}{isError && <div className="admin-form-error">Unable to load settings.</div>}<form className="form-card" onSubmit={(e) => { e.preventDefault(); update.mutate(form); }}><label>Support email<input value={form.SupportEmail} onChange={(e) => set("SupportEmail", e.target.value)} /></label><label>Commission percentage<input type="number" min="0" max="100" step="0.01" value={form.CommissionPercentage} onChange={(e) => set("CommissionPercentage", e.target.value)} /></label><label>PayPal email<input value={form.PayPalEmail} onChange={(e) => set("PayPalEmail", e.target.value)} /></label><label>Default currency<input value={form.DefaultCurrency} onChange={(e) => set("DefaultCurrency", e.target.value)} /></label><button className="button button--accent" disabled={update.isPending}>Save settings</button><button type="button" className="button button--quiet" onClick={load}>Load current values</button></form></section>;
}
