import { useTermsAndConditions } from "./termsAndConditionQueries.js";
import PublicPageFrame from "../PublicPageFrame.jsx";

export default function TermsAndConditions() {
  const { data, isLoading, isError } = useTermsAndConditions();
  const items = data?.items ?? data?.Items ?? [];
  return <PublicPageFrame><section className="editorial-hero editorial-hero--compact"><span className="app-shell__eyebrow">Terms</span><h1>The agreement behind the work.</h1><p>Read the platform terms that keep projects, payments, and responsibilities clear.</p></section><section className="legal-layout"><aside><span className="app-shell__eyebrow">On this page</span>{items.map((item) => <a href={`#terms-${item.id}`} key={item.id}>{item.heading}</a>)}</aside><div className="legal-content">{isLoading && <div className="content-state">Loading terms…</div>}{isError && <div className="content-state">Terms content is temporarily unavailable.</div>}{!isLoading && !isError && items.map((item, index) => <article id={`terms-${item.id}`} className="legal-section" key={item.id}><span>0{index + 1}</span><h2>{item.heading}</h2><p>{item.description}</p></article>)}</div></section></PublicPageFrame>;
}