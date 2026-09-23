import { Link } from "react-router-dom";
import { useRecommendedFreelancers } from "../homePageQueries.js";
import HomeFreelancerCard from "./HomeFreelancerCard.jsx";

export default function TopFreelancers() {
  const { data, isLoading } = useRecommendedFreelancers();
  const people = data?.items ?? data?.Items ?? data ?? [];
  return <section className="content-section"><div className="section-heading section-heading--split"><div><span className="app-shell__eyebrow">People worth meeting</span><h2>Start with a point of view.</h2></div><Link className="text-link" to="/find-talents">Browse all talent</Link></div>{isLoading && <div className="content-state">Loading recommendations...</div>}{!isLoading && people.length === 0 && <div className="content-state">Recommendations will appear as the marketplace grows.</div>}{people.length > 0 && <div className="people-grid">{people.slice(0, 4).map((person) => <HomeFreelancerCard freelancer={person} key={person.id ?? person.Id} />)}</div>}</section>;
}