import { usePrivacyPolicy } from "./privacyPolicyQueries.js";
import PublicPageFrame from "../PublicPageFrame.jsx";

export default function PrivacyPolicies() {
  const { data, isLoading, isError } = usePrivacyPolicy();
  const items = data?.items ?? data?.Items ?? [];
  return <PublicPageFrame><section className="editorial-hero editorial-hero--compact"><span className="app-shell__eyebrow">Privacy</span><h1>Your data deserves plain language.</h1><p>Read how WebOfWork approaches account, project, and platform information.</p></section><section className="legal-layout"><aside><span className="app-shell__eyebrow">On this page</span>{items.map((item) => <a href={`#privacy-${item.id}`} key={item.id}>{item.heading}</a>)}</aside><div className="legal-content">{isLoading && <div className="content-state">Loading policy…</div>}{isError && <div className="content-state">Privacy content is temporarily unavailable.</div>}{!isLoading && !isError && items.map((item, index) => <article id={`privacy-${item.id}`} className="legal-section" key={item.id}><span>0{index + 1}</span><h2>{item.heading}</h2><p>{item.description}</p></article>)}</div></section></PublicPageFrame>;
}