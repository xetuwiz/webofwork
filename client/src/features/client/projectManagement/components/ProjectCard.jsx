import React from "react";
import { ClipboardList, Clock, FileText, ChevronRight } from "lucide-react";
import { useNavigate } from "react-router-dom";
import { useModal } from "../../../../hooks/modal/useModalStore";

export default function ProjectCard({ job }) {
  const navigate = useNavigate();
  const { openModal } = useModal();

  const isProjectInProgress = job.status === "in_progress" || job.status === "in-progress" || job.status === 'completed';
  
  const renderBudget = () => {
    const fixed = job.fixed_price ? Number(job.fixed_price).toLocaleString() : "0";
    const min = job.min_budget ? Number(job.min_budget).toLocaleString() : "0";
    const max = job.max_budget ? Number(job.max_budget).toLocaleString() : "0";

    if (job.pricing_type === "fixed") {
      return <span className="text-sm md:text-base text-fg font-bold">${fixed}</span>;
    } else {
      return <span className="text-sm md:text-base text-fg font-bold">${min} - ${max}</span>;
    }
  };

  return (
    <div className="bg-surface border border-line rounded-xl shadow-sm p-5 md:p-6 flex flex-col md:flex-row gap-5 md:gap-6 hover:shadow-md transition-shadow">
      <div className="flex-1 min-w-0">
        <div className="flex items-center flex-wrap gap-y-2 gap-x-3 mb-3">
          <span className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-[10px] md:text-xs font-bold uppercase tracking-wider ${
            isProjectInProgress ? "bg-brand-soft text-brand" : "bg-success-soft text-success-soft-fg"
          }`}>
            {job.status === "in_progress" ? "In Progress" : job.status}
          </span>
          <div className="flex items-center text-xs md:text-sm text-fg-subtle gap-2">
            <span>{new Date(job.created_at).toLocaleDateString()}</span>
            <span className="h-1 w-1 rounded-full bg-surface-inset" />
            <span className="font-medium text-fg-muted">{job.category_name}</span>
          </div>
        </div>

        <h2 
          className="text-lg md:text-xl font-bold text-fg mb-2 leading-tight capitalize cursor-pointer hover:text-brand transition-colors flex items-center justify-between group" 
          onClick={() => navigate(`/my-projects/${job.id}`)}
        >
          <span className="truncate">{job.title}</span>
          <ChevronRight className="w-5 h-5 md:hidden text-fg-subtle group-hover:text-brand" />
        </h2>

        <p className="text-sm md:text-base text-fg-muted mb-4 leading-relaxed line-clamp-2 md:line-clamp-3">
          {job.description}
        </p>

        <div className="grid grid-cols-2 md:flex md:items-center gap-4 md:gap-8 pt-2 border-t border-line-soft md:border-none">
          <div className="flex flex-col md:flex-row md:items-center gap-0.5 md:gap-1.5">
            {renderBudget()}
            <span className="text-[10px] md:text-sm text-fg-subtle font-medium uppercase md:capitalize">
              {job.pricing_type === "fixed" ? "Fixed Price" : "Budget Range"}
            </span>
          </div>

          <div className="flex items-center gap-2">
            <div className="p-1.5 bg-surface-2 rounded-md md:bg-transparent md:p-0">
              <ClipboardList className="w-4 h-4 text-fg-subtle" />
            </div>
            <span className="text-xs md:text-sm text-fg-muted font-medium">{job.proposals_count || 0} Proposals</span>
          </div>

          <div className="flex items-center gap-2 col-span-2 md:col-span-1">
            <div className="p-1.5 bg-surface-2 rounded-md md:bg-transparent md:p-0">
              <Clock className="w-4 h-4 text-fg-subtle" />
            </div>
            <span className="text-xs md:text-sm text-fg-muted font-medium">{job.delivery_days} Days Delivery</span>
          </div>
        </div>
      </div>

      <div className="flex flex-col sm:flex-row md:flex-col justify-center gap-2 md:gap-3 w-full md:w-44 pt-4 md:pt-0 border-t md:border-none border-line-soft">
        {isProjectInProgress ? (
          <>
            <button 
              onClick={() => navigate(`/contracts/${job.contract_id || ''}`)} 
              className="flex-1 md:flex-none h-11 px-4 min-h-11 bg-brand shrink-0  text-brand-fg text-sm font-bold rounded-lg hover:bg-brand-hover transition-colors shadow-sm flex items-center justify-center gap-2 active:scale-[0.98]"
            >
              <FileText className="w-4 h-4" />
              View Contract
            </button>
            <button 
              onClick={() => navigate(`/my-projects/${job.id}`)} 
              className="flex-1 md:flex-none min-h-11 h-11 px-4 border border-line-strong bg-surface text-fg-muted text-sm font-bold rounded-lg hover:bg-surface-2 transition-colors active:scale-[0.98]"
            >
              Details
            </button>
          </>
        ) : (
          <div className="grid grid-cols-2 md:flex md:flex-col gap-2 w-full">
            <button 
              onClick={() => navigate(`/my-projects/${job.id}/proposals`)} 
              className="col-span-2 md:col-span-1 h-11 px-4 bg-brand text-brand-fg text-sm font-bold rounded-lg hover:bg-brand-hover transition-colors shadow-sm active:scale-[0.98]"
            >
              View Proposals
            </button>
            <button 
              onClick={() => navigate(`/my-projects/${job.id}/edit`)} 
              className="h-11 px-4 border border-line-strong bg-surface text-fg-muted text-sm font-bold rounded-lg hover:bg-surface-2 transition-colors active:scale-[0.98]"
            >
              Edit
            </button>
            <button 
              onClick={() => openModal('delete-project', { projectId: job.id })} 
              className="h-11 px-4 border border-danger/30 bg-surface text-danger text-sm font-bold rounded-lg hover:bg-danger-soft transition-colors active:scale-[0.98]"
            >
              Delete
            </button>
          </div>
        )}
      </div>
    </div>
  );
}