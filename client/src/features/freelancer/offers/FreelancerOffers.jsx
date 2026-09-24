import { useState } from "react";
import { useFreelancerOffers } from "./offersQueries.js";
import { useModal } from "../../../hooks/modal/useModalStore.jsx";

export default function FreelancerOffers() {
  const { data, isLoading, isError } = useFreelancerOffers();
  const { openModal } = useModal();
  const offers = data?.results || (Array.isArray(data) ? data : []);
  if (isLoading) return <section className="content-section"><p>Loading offers…</p></section>;
  if (isError) return <section className="content-section"><p role="alert">Unable to load offers.</p></section>;
  return <section className="content-section"><span className="app-shell__eyebrow">Freelancer workspace</span><h1>Project offers</h1><p>Review and sign contracts to begin work.</p><div className="card-list">{offers.map((offer) => <article className="card" key={offer.id}><div><strong>{offer.projectTitle || offer.project_title || `Offer #${offer.id}`}</strong><p>{offer.clientName || offer.client_name || "Client"}</p></div><p>{offer.status}</p><button className="button button--accent" onClick={() => openModal("offer-modal", { offer })}>Review & sign</button></article>)}</div>{offers.length === 0 && <p>No offers are waiting for you.</p>}</section>;
}
