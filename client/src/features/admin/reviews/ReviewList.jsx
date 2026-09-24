import { useState } from "react";
import { useAdminReviews } from "./reviewQueries.js";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { deleteReview } from "./reviewApi.js";

export default function ReviewList() {
  const [type, setType] = useState("");
  const { data, isLoading, isError } = useAdminReviews(type ? { type } : {});
  const client = useQueryClient();
  const remove = useMutation({ mutationFn: deleteReview, onSuccess: () => client.invalidateQueries({ queryKey: ["admin-reviews"] }) });
  const reviews = data?.results || (Array.isArray(data) ? data : []);
  return <section className="admin-subsection"><div className="admin-subsection__heading"><div><span className="app-shell__eyebrow">Trust & safety</span><h1>Review management</h1></div><span>{reviews.length} reviews</span></div><div className="button-row">{["", "Freelancer", "Client"].map((item) => <button key={item || "all"} className={`button ${type === item ? "button--accent" : "button--quiet"}`} onClick={() => setType(item)}>{item || "All"}</button>)}</div>{isLoading && <div className="admin-loading">Loading reviews...</div>}{isError && <div className="admin-form-error" role="alert">Unable to load reviews.</div>}{!isLoading && !isError && reviews.length === 0 && <div className="admin-empty">No reviews found.</div>}<div className="admin-payout-list">{reviews.map((review) => <article className="admin-payout-row" key={review.id}><div><strong>{review.reviewerName || review.reviewerId}</strong><small>{review.comment}</small></div><div><strong>{review.rating} / 5</strong><button className="button button--quiet" onClick={() => remove.mutate(review.id)} disabled={remove.isPending}>Delete</button></div></article>)}</div></section>;
}
