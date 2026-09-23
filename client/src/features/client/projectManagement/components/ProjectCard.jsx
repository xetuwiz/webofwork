import { useState } from "react";
import { Link } from "react-router-dom";
import { useDeleteProject } from "../projectMutations.js";
import ProjectDeleteModal from "./ProjectDeleteModal.jsx";

export default function ProjectCard({ project }) {
  const [deleting, setDeleting] = useState(false);
  const mutation = useDeleteProject();
  if (!project) return null;
  const budget = project.fixedPrice ?? project.minPrice ?? project.budgetMin ?? 0;
  return <><article className="project-card"><div><span className="app-shell__eyebrow">{project.status ?? "Open"}</span><h3>{project.title}</h3><p>{project.description}</p></div><div className="project-card__meta"><span>From ${Number(budget).toFixed(2)}</span><span>{project.expectedDays ?? project.deadlineDays ?? 0} days</span></div><div className="project-card__actions"><Link className="text-link" to={`/my-projects/${project.id}`}>View project</Link><button type="button" onClick={() => setDeleting(true)}>Archive</button></div></article><ProjectDeleteModal project={deleting ? project : null} pending={mutation.isPending} onClose={() => setDeleting(false)} onConfirm={() => mutation.mutate({ id: project.id }, { onSuccess: () => setDeleting(false) })} /></>;
}