import { useParams } from "react-router-dom";
import { Star } from "lucide-react";
import { useFreelancerReviews } from "./freelancerProfileQueries.js";

export default function AllFreelancerReviews() {
  const { id } = useParams();
  const { data: reviews = [], isLoading, isError, error } = useFreelancerReviews(id);

  if (isLoading) return <div className="p-8 text-sm text-slate-500">Loading reviews...</div>;
  if (isError) return <div className="p-8 text-sm text-red-500">{error?.response?.data?.detail || "Unable to load reviews."}</div>;

  return (
    <div className="mx-auto max-w-4xl space-y-6 px-4 py-10">
      <div className="flex items-center justify-between">
        <h1 className="text-3xl font-bold text-slate-900">Reviews</h1>
        <span className="text-sm text-slate-500">{reviews.length} total</span>
      </div>

      <div className="space-y-4">
        {(reviews.length > 0 ? reviews : []).map((review) => (
          <div key={review.id} className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
            <div className="mb-3 flex items-center justify-between gap-3">
              <div>
                <p className="font-semibold text-slate-800">{review.reviewer_name || "Client"}</p>
                <p className="text-xs text-slate-500">{review.project_title || "Project review"}</p>
              </div>
              <div className="flex items-center gap-1">
                {[...Array(5)].map((_, index) => (
                  <Star key={`${review.id}-${index}`} className={`h-4 w-4 ${index < (review.rating ?? 0) ? "fill-yellow-400 text-yellow-400" : "text-slate-200"}`} />
                ))}
              </div>
            </div>
            <p className="text-sm leading-7 text-slate-600">{review.comment || "This client did not leave a written review."}</p>
          </div>
        ))}
      </div>
    </div>
  );
}
