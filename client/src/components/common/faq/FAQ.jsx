import { useState } from "react";
import { useFAQs } from "./faqQuries.js";
import PublicPageFrame from "../PublicPageFrame.jsx";

const categories = [["", "All"], ["general", "General"], ["freelancer", "Freelancers"], ["client", "Clients"], ["payment", "Payments"]];

export default function FAQ() {
  const [category, setCategory] = useState("");
  const [openId, setOpenId] = useState(null);
  const { data, isLoading, isError } = useFAQs({ category });
  const items = data?.items ?? data?.Items ?? [];

  return <PublicPageFrame><section className="editorial-hero editorial-hero--compact"><span className="app-shell__eyebrow">Answers, without the maze</span><h1>Frequently asked questions.</h1><p>Find practical answers about projects, professionals, payments, and support.</p></section><section className="content-section faq-section"><div className="filter-row">{categories.map(([value, label]) => <button key={value} type="button" className={category === value ? "filter-chip filter-chip--active" : "filter-chip"} onClick={() => { setCategory(value); setOpenId(null); }}>{label}</button>)}</div>{isLoading && <div className="content-state">Loading answers…</div>}{isError && <div className="content-state">Answers are temporarily unavailable.</div>}{!isLoading && !isError && items.length === 0 && <div className="content-state">No questions are available for this category.</div>}<div className="faq-list">{items.map((item) => <article className="faq-item" key={item.id}><button type="button" aria-expanded={openId === item.id} onClick={() => setOpenId(openId === item.id ? null : item.id)}><span>{item.question}</span><b>{openId === item.id ? "−" : "+"}</b></button>{openId === item.id && <p>{item.answer}</p>}</article>)}</div></section></PublicPageFrame>;
}