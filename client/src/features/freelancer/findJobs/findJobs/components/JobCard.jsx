import React from 'react';
import { CircleDollarSign, Clock, Send } from 'lucide-react';
import { toastSuccess } from "../../../../utils/toast";

export default function JobCard({id, category, title, description, priceRange, duration, skills }) {
const handleShare = (e, projectId) => {
    e.preventDefault();
    e.stopPropagation();

    const url = `${window.location.origin}/freelancer/jobs/${projectId}`;
    navigator.clipboard.writeText(url);
    toastSuccess("Project link copied");
  };
  return (
    <div className="bg-surface rounded-xl p-8 shadow-sm border border-transparent hover:border-brand-line transition-all flex flex-col h-full">
      <div className="flex justify-between items-start mb-4">
        <span className="bg-surface-2 text-fg-subtle px-4 py-1 rounded-full text-[11px] font-medium">
          {category}
        </span>
        <button onClick={(e)=>handleShare(e,id)} className="p-2.5 bg-brand text-brand-fg rounded-md hover:bg-brand-hover transition-colors">
          <Send className="w-4 h-4 fill-current" />
        </button>
      </div>

      <h3 className="text-[18px] font-bold text-fg leading-tight mb-3">
        {title}
      </h3>
      
      <p className="text-[13px] text-fg-subtle leading-relaxed mb-6 line-clamp-2">
        {description}
      </p>

      <div className="flex items-center gap-6 mb-6">
        <div className="flex items-center gap-2 text-fg">
          <CircleDollarSign className="w-4 h-4 text-fg-subtle" />
          <span className="text-[14px] font-bold">{priceRange}</span>
        </div>
        <div className="flex items-center gap-2 text-fg-subtle">
          <Clock className="w-4 h-4 text-fg-subtle" />
          <span className="text-[13px] font-medium">{duration}</span>
        </div>
      </div>

      <div className="flex flex-wrap gap-2">
        {skills.map((skill, index) => (
          <span key={index} className="bg-surface text-fg-subtle px-4 py-1 rounded-md text-[11px] font-medium border border-line-soft">
            {skill}
          </span>
        ))}
      </div>
    </div>
  );
}