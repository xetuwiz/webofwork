import { useState } from "react";
import { createSupportTicket } from "./commonApi.js";
import toast from "react-hot-toast";

export default function SupportPage() {
  const [form, setForm] = useState({ subject: "", message: "" });
  const [pending, setPending] = useState(false);
  const set = (key, value) => setForm((current) => ({ ...current, [key]: value }));
  const submit = async (event) => { event.preventDefault(); if (!form.subject.trim() || !form.message.trim()) return toast.error("Please fill in all required fields."); setPending(true); try { await createSupportTicket({ subject: form.subject.trim(), message: form.message.trim() }); toast.success("Support ticket created successfully."); setForm({ subject: "", message: "" }); } catch (error) { toast.error(error?.response?.data?.error || "Failed to send message."); } finally { setPending(false); } };
  return <section className="content-section"><span className="app-shell__eyebrow">Support</span><h1>Contact support</h1><p>Send us a message and our team will respond to your registered email address.</p><form className="form-card" onSubmit={submit}><label>Subject<input required value={form.subject} onChange={(e) => set("subject", e.target.value)} /></label><label>Message<textarea required rows={6} value={form.message} onChange={(e) => set("message", e.target.value)} /></label><button className="button button--accent" disabled={pending}>{pending ? "Sending..." : "Send message"}</button></form></section>;
}
