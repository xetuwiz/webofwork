import React from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useProjectDetails } from './projectQueries';
import FindTalentButton from '../../aiworkflow/FindTalentButton';
import { Edit, Users, Clock, Box, DollarSign, FileText, ChevronLeft } from 'lucide-react';

export default function ClientJobDetail() {
  const { id } = useParams();
  const navigate = useNavigate();
  const { data, isLoading, isError, error } = useProjectDetails(id);

  const isProjectInProgress = data?.status === "in_progress" || data?.status === "in-progress" || data?.status === 'completed';

  if (isLoading) return <div className="flex justify-center items-center h-screen text-fg-muted font-medium">Loading Project details...</div>;
  if (isError) return <div className="flex justify-center items-center h-screen text-danger-soft-fg font-medium">Error: {error.message}</div>;

  const formatList = (text) => {
    if (!text) return [];
    return text.split('\n').filter(item => item.trim() !== "");
  };

  return (
    <div className="min-h-screen bg-surface-2/30">
      <div className="bg-surface border-b border-line-soft mb-6 md:mb-8">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-4 md:py-6">
          <button 
            onClick={() => navigate('/my-projects')}
            className="flex items-center gap-2 text-fg-subtle hover:text-brand transition-colors text-sm font-bold mb-4"
          >
            <ChevronLeft className="w-4 h-4" />
            Back to Projects
          </button>
          
          <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
            <div className="space-y-2">
              <div className="flex items-center gap-3">
                <span className={`px-3 py-1 rounded-full text-[10px] md:text-xs font-black uppercase tracking-wider ${
                  isProjectInProgress ? 'bg-brand-soft text-brand' : 'bg-success-soft text-success'
                }`}>
                  {data.status === 'in_progress' ? 'In Progress' : data.status}
                </span>
                <span className="text-xs text-fg-subtle font-bold uppercase tracking-widest">{data.category_name || 'General'}</span>
              </div>
              <h1 className="text-2xl md:text-4xl font-black text-fg leading-tight">
                {data.title}
              </h1>
            </div>
          </div>
        </div>
      </div>

      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 pb-20">
        <div className="flex flex-col lg:flex-row gap-8">
          
          <div className="w-full lg:w-2/3 space-y-8 md:space-y-10">
            <section className="bg-surface p-6 md:p-8 rounded-2xl border border-line-soft shadow-sm">
              <h2 className="text-lg md:text-xl font-black text-fg mb-4 flex items-center gap-2">
                <div className="w-1.5 h-6 bg-brand rounded-full" />
                Project Description
              </h2>
              <p className="text-sm md:text-base wrap-break-word text-fg-muted leading-relaxed whitespace-pre-wrap">
                {data.description}
              </p>
            </section>

            <div className="grid grid-cols-1 md:grid-cols-2 gap-6 md:gap-8">
              <section className="bg-surface p-6 md:p-8 rounded-2xl border border-line-soft shadow-sm">
                <h2 className="text-lg md:text-xl font-black text-fg mb-5">Deliverables</h2>
                <ul className="space-y-4">
                  {formatList(data.expected_deliverables).map((item, index) => (
                    <li key={index} className="flex items-start gap-3 text-sm md:text-base text-fg-muted group">
                      <div className="mt-1.5 w-1.5 h-1.5 rounded-full bg-brand-soft shrink-0 group-hover:scale-125 transition-transform" />
                      <span className='wrap-anywhere'>{item}</span>
                    </li>
                  ))}
                </ul>
              </section>

              <section className="bg-surface p-6 md:p-8 rounded-2xl border border-line-soft shadow-sm">
                <h2 className="text-lg md:text-xl font-black text-fg mb-5">Requirements</h2>
                <ul className="space-y-4">
                  {formatList(data.requirements).map((item, index) => (
                    <li key={index} className="flex items-start gap-3 text-sm md:text-base text-fg-muted group">
                      <div className="mt-1.5 w-1.5 h-1.5 rounded-full bg-brand-soft shrink-0 group-hover:scale-125 transition-transform" />
                      <span className='wrap-anywhere'>{item}</span>
                    </li>
                  ))}
                </ul>
              </section>
            </div>

            <section className="bg-surface p-6 md:p-8 rounded-2xl border border-line-soft shadow-sm">
              <h2 className="text-lg md:text-xl font-black text-fg mb-4">Required Skills</h2>
              <div className="flex flex-wrap gap-2 md:gap-3">
                {data.skills?.map((skill, index) => (
                  <span key={index} className="bg-surface-2 border border-line-soft text-fg-muted px-4 py-1.5 rounded-xl text-xs md:text-sm font-bold transition-all hover:bg-surface hover:border-brand-line hover:text-brand">
                    {skill}
                  </span>
                ))}
              </div>
            </section>
          </div>

          <div className="w-full lg:w-1/3 space-y-6">
            <div className="bg-surface border border-line-soft rounded-2xl p-6 md:p-8 shadow-sm sticky top-8">
              <h3 className="text-lg font-black text-fg mb-6 uppercase tracking-wider text-[10px]">Project Overview</h3>
              <div className="space-y-6">
                <div className="flex items-center justify-between group">
                  <div className="flex items-center gap-4">
                    <div className="p-2.5 bg-success-soft rounded-xl text-success group-hover:bg-success group-hover:text-success-fg transition-all">
                      <DollarSign className="w-5 h-5" />
                    </div>
                    <div>
                      <p className="text-[10px] font-black text-fg-subtle uppercase tracking-widest">Budget</p>
                      <p className="font-bold text-fg md:text-lg">
                        {data.pricing_type === 'fixed' ? `$${data.fixed_price}` : `$${data.min_budget} - $${data.max_budget}`}
                      </p>
                    </div>
                  </div>
                </div>

                <div className="flex items-center justify-between group">
                  <div className="flex items-center gap-4">
                    <div className="p-2.5 bg-brand-soft rounded-xl text-brand group-hover:bg-brand-hover group-hover:text-brand-fg transition-all">
                      <Clock className="w-5 h-5" />
                    </div>
                    <div>
                      <p className="text-[10px] font-black text-fg-subtle uppercase tracking-widest">Timeline</p>
                      <p className="font-bold text-fg md:text-lg">{data.delivery_days} Days</p>
                    </div>
                  </div>
                </div>

                <div className="flex items-center justify-between group">
                  <div className="flex items-center gap-4">
                    <div className="p-2.5 bg-warning-soft rounded-xl text-warning-soft-fg group-hover:bg-warning group-hover:text-warning-fg transition-all">
                      <Box className="w-5 h-5" />
                    </div>
                    <div>
                      <p className="text-[10px] font-black text-fg-subtle uppercase tracking-widest">Pricing Type</p>
                      <p className="font-bold text-fg md:text-lg capitalize">{data.pricing_type || 'Fixed'}</p>
                    </div>
                  </div>
                </div>

                <div className="pt-6 border-t border-line-soft flex flex-col gap-3">
                  {isProjectInProgress ? (
                    <button 
                      onClick={() => navigate(`/contracts/${data.contract_id}`)}
                      className="w-full flex items-center justify-center gap-3 bg-brand hover:bg-brand-hover text-brand-fg font-black py-4 rounded-xl transition-all shadow-xl shadow-blue-100 active:scale-[0.98]"
                    >
                      <FileText className="w-5 h-5" />
                      View Contract
                    </button>
                  ) : (
                    <>
                      <button 
                        onClick={() => navigate(`/my-projects/${id}/proposals`)}
                        className="w-full flex items-center justify-center gap-3 bg-brand hover:bg-brand-hover text-brand-fg font-black py-4 rounded-xl transition-all shadow-xl shadow-blue-100 active:scale-[0.98]"
                      >
                        <Users className="w-5 h-5" />
                        View Proposals
                      </button>
                      
                      <button 
                        onClick={() => navigate(`/my-projects/${id}/edit`)}
                        className="w-full flex items-center justify-center gap-3 bg-surface border border-line hover:bg-surface-2 text-fg-muted font-bold py-4 rounded-xl transition-all active:scale-[0.98]"
                      >
                        <Edit className="w-5 h-5" />
                        Edit Details
                      </button>

                      {/* Only while the project is still open. Once it is in
                          progress the freelancer set is settled -- a contract
                          exists -- and re-running matching would produce a
                          shortlist for a decision that has already been made. */}
                      {data.status === 'open' && (
                        <div className="pt-1">
                          <FindTalentButton projectId={id} />
                        </div>
                      )}
                    </>
                  )}
                </div>
              </div>
            </div>
          </div>

        </div>
      </div>
    </div>
  );
}