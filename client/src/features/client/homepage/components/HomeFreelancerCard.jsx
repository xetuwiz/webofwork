import { Link } from "react-router-dom";

export default function HomeFreelancerCard({ freelancer }) {
  if (!freelancer) return null;

  const name = freelancer.fullName ?? freelancer.FullName ?? freelancer.name ?? "Independent professional";
  const skills = (freelancer.skills ?? freelancer.Skills ?? []).slice(0, 3);
  const rating = Number(freelancer.averageRating ?? freelancer.AverageRating ?? 0);

  return (
    <article className="freelancer-card">
      <div className="freelancer-card__identity">
        <span className="freelancer-card__avatar" aria-hidden="true">{name.slice(0, 1).toUpperCase()}</span>
        <div><h3>{name}</h3><p>{freelancer.tagline ?? freelancer.Tagline ?? "Ready for the next thoughtful project."}</p></div>
      </div>
      <p className="freelancer-card__bio">{freelancer.bio ?? freelancer.Bio ?? "A capable independent professional for focused work."}</p>
      <div className="freelancer-card__meta">
        <span>{rating > 0 ? `${rating.toFixed(1)} rating` : "New profile"}</span>
        <span>{skills.length ? skills.map((skill) => typeof skill === "string" ? skill : skill.name ?? skill.Name).join(" / ") : "Open to projects"}</span>
      </div>
      <Link className="text-link" to={`/find-talents/${freelancer.id ?? freelancer.Id}`}>View profile</Link>
    </article>
  );
}