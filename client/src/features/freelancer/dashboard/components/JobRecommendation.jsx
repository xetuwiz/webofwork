import React from 'react'
import { useRecommendedProjects } from '../dashboardQueries'
import { Briefcase } from 'lucide-react'
import { Link } from 'react-router-dom';

function JobRecommendation() {
    const { data, isLoading } = useRecommendedProjects();
    const jobList = data?.results || []

    if (isLoading) {
        return (
            <div className="mb-8 w-full">
                <h2 className="text-xl md:text-[25px] font-semibold text-fg leading-7 mb-4 px-1">
                    Recommended Jobs
                </h2>
                <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4 md:gap-6">
                    {[...Array(3)].map((_, i) => (
                        <div key={i} className="h-64 bg-surface-3 shimmer rounded-xl border border-line-soft"></div>
                    ))}
                </div>
            </div>
        )
    }

    return (
        <div className="mb-8 w-full">
            <h2 className="text-xl md:text-[25px] font-semibold text-fg leading-7 mb-4 px-1">
                Recommended Jobs
            </h2>

            {jobList.length > 0 ? (
                <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4 md:gap-6">
                    {jobList.map((job, index) => (
                        <div key={job.id || index} className="bg-surface rounded-xl border border-line-soft shadow-sm flex flex-col h-full hover:shadow-md transition-shadow">
                            <div className="p-5 md:p-6 lg:p-8 flex flex-col h-full">
                                <div className="mb-4">
                                    <span className="inline-block bg-brand-soft text-brand px-3 py-1 rounded-full text-[11px] md:text-[12px] font-bold uppercase tracking-wider">
                                        {job.category_name || 'General'}
                                    </span>
                                </div>
                                
                                <h3 className="text-base md:text-[18px] lg:text-[20px] font-bold text-fg leading-tight mb-3 line-clamp-2 min-h-[2.5rem] md:min-h-0">
                                    {job.title}
                                </h3>
                                
                                <p className="text-xs md:text-sm text-fg-muted leading-relaxed mb-6 line-clamp-3 flex-grow">
                                    {job.description}
                                </p>

                                <div className="flex items-center justify-between pb-5 border-b border-line-soft">
                                    <div>
                                        <p className="text-[11px] md:text-[13px] text-fg-subtle mb-0.5 font-bold uppercase tracking-tighter">Budget</p>
                                        <p className="text-sm md:text-base font-black text-fg">${job.budget}</p>
                                    </div>
                                    <div className="text-right">
                                        <p className="text-[11px] md:text-[13px] text-fg-subtle mb-0.5 font-bold uppercase tracking-tighter">Delivery</p>
                                        <p className="text-sm md:text-base font-bold text-fg-muted">{job.delivery_days} Days</p>
                                    </div>
                                </div>

                                <div className="flex flex-wrap gap-1.5 mt-5 mb-6">
                                    {job.skills?.slice(0, 3).map((skill, i) => (
                                        <span key={i} className="bg-surface-2 text-fg-muted px-2.5 py-1 rounded-lg border border-line-soft text-[10px] md:text-[11px] font-bold">
                                            {skill}
                                        </span>
                                    ))}
                                    {job.skills?.length > 3 && (
                                        <span className="text-[10px] font-bold text-fg-subtle self-center">+{job.skills.length - 3}</span>
                                    )}
                                </div>

                                <Link 
                                    to={`/freelancer/jobs/${job.id}`} 
                                    className="inline-flex items-center justify-center w-full md:w-auto md:justify-start text-xs font-black text-brand hover:text-brand transition-colors mt-auto group"
                                >
                                    View Details 
                                    <span className="ml-1 group-hover:translate-x-1 transition-transform">→</span>
                                </Link>
                            </div>
                        </div>
                    ))}
                </div>
            ) : (
                <div className="bg-surface rounded-2xl p-8 md:p-12 border-2 border-dashed border-line flex flex-col items-center justify-center text-center">
                    <div className="w-14 h-14 bg-surface-2 rounded-2xl flex items-center justify-center mb-4 rotate-3">
                        <Briefcase className="w-7 h-7 text-fg-subtle -rotate-3" />
                    </div>
                    <p className="text-fg-muted font-bold text-sm md:text-base">
                        No recommended jobs yet.
                    </p>
                    <p className="text-xs text-fg-subtle mt-1 max-w-[200px] md:max-w-none">
                        Update your profile skills to get better recommendations.
                    </p>
                </div>
            )}
        </div>
    )
}

export default JobRecommendation
