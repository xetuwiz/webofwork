import React, { useState, useEffect } from "react";
import { Link, useParams } from "react-router-dom";
import { useContractDetail } from "../../freelancer/contracts/contractsQueries";
import { useRequestRevision, useUpdateContractStatus, useCreateReview } from "./contractMutations";
import { useChatActions } from "../../../components/chat/chatMutations";
import {
  FileText, Clock, Download, MessageSquare, ShieldAlert,
  ExternalLink, X, AlertCircle, CheckCircle2, ShieldCheck,
  Scale, Info, History, Landmark, User, Tag, ListChecks, Target, Layers, Star
} from "lucide-react";
import { useModal } from "../../../hooks/modal/useModalStore";
import { formatDateDMY } from "../../../utils/date";
import { toastError, toastSuccess } from "../../../utils/toast";
import Dialog, { DialogClose } from "../../../components/common/Dialog";
import { Field } from "../../../components/common/Field";

export default function ClientContractDetail() {
  const { id } = useParams();
  const { data: contract, isLoading, isError } = useContractDetail(id);
  const requestRevisionMutation = useRequestRevision();
  const updateStatusMutation = useUpdateContractStatus();
  const createReviewMutation = useCreateReview();
  const { getRoomMutation } = useChatActions();
  const { openModal } = useModal();

  const [timeLeft, setTimeLeft] = useState({ days: 0, hours: 0, percentage: 0 });
  const [isRevisionModalOpen, setIsRevisionModalOpen] = useState(false);
  const [revisionReason, setRevisionReason] = useState("");
  
  const [rating, setRating] = useState(5);
  const [comment, setComment] = useState("");

  useEffect(() => {
    if (contract?.freelancer_signed_at && contract?.delivery_days) {
      const calculateTime = () => {
        const start = new Date(contract.freelancer_signed_at).getTime();
        const durationMs = contract.delivery_days * 24 * 60 * 60 * 1000;
        const end = start + durationMs;
        const now = new Date().getTime();
        const difference = end - now;
        const totalDuration = end - start;

        if (difference > 0) {
          const days = Math.floor(difference / (1000 * 60 * 60 * 24));
          const hours = Math.floor((difference % (1000 * 60 * 60 * 24)) / (1000 * 60 * 60));
          const elapsed = now - start;
          const percentage = Math.min(Math.max((elapsed / totalDuration) * 100, 0), 100);
          setTimeLeft({ days, hours, percentage });
        } else {
          setTimeLeft({ days: 0, hours: 0, percentage: 100 });
        }
      };
      calculateTime();
      const timer = setInterval(calculateTime, 60000);
      return () => clearInterval(timer);
    }
  }, [contract]);

  const handleMessageFreelancer = async () => {
    try {
      const room = await getRoomMutation.mutateAsync(contract.freelancer_user_id ?? contract.freelancer_id);
      openModal("messages", { initialRoomId: room.id });
    } catch (error) {
      toastError("Could not open chat. Please try again.");
    }
  };

  const handleRevisionSubmit = () => {
    if (revisionReason.length < 10) {
      toastError("Please provide a more detailed reason.");
      return;
    }
    requestRevisionMutation.mutate(
      { contractId: id, reason: revisionReason },
      {
        onSuccess: () => {
          setIsRevisionModalOpen(false);
          setRevisionReason("");
          toastSuccess('Request submitted.');
        }
      }
    );
  };

  const handleApproveOrder = () => {
    openModal("approve-contract", {
      projectName: contract.project_title,
      freelancerName: contract.freelancer_name,
      amount: contract.total_amount,
      onApprove: () => {
        updateStatusMutation.mutate({
          contractId: contract.id,
          status: 'completed'
        });
      }
    });
  };

  const handleSubmitReview = () => {
    if (!comment.trim()) {
      toastError("Please share your experience in a comment.");
      return;
    }
    createReviewMutation.mutate({
      contract: contract.id,
      reviewee: contract.freelancer_id,
      review_type: 'freelancer',
      rating,
      comment
    });
  };

  if (isLoading) return <div className="min-h-screen flex items-center justify-center font-medium text-fg-subtle">Loading Contract Interface...</div>;
  if (isError) return <div className="min-h-screen flex items-center justify-center text-danger-soft-fg font-bold">Error loading contract details.</div>;

  const dueDate = new Date(new Date(contract.freelancer_signed_at).getTime() + (contract.delivery_days * 24 * 60 * 60 * 1000));
  const pendingRevision = contract.revisions?.find(r => r.status === 'pending');
  const acceptedRevision = contract.revisions?.find(r => r.status === 'accepted');
  const dispute = contract.dispute_details;
  const existingReview = contract.reviews?.client_review;
  const freelancerReview = contract.reviews?.freelancer_review
  const isFreelancerDisputed = dispute && dispute?.raised_by_id === contract.freelancer_id;

  return (
    <div className="min-h-screen bg-surface-2 pb-20">
      <div className="max-w-page-2xl mx-auto px-4 sm:px-6 lg:px-12 py-10 lg:py-14">

        <div className="mb-10 flex flex-col md:flex-row md:items-end justify-between gap-6 border-b border-line pb-8">
          <div className="break-words overflow-hidden">
            <div className="flex items-center gap-2 mb-2">
              <Landmark className="w-5 h-5 text-brand shrink-0" />
              <span className="text-xs font-bold text-brand uppercase tracking-widest">Escrow Protected</span>
            </div>
            <h1 className="text-2xl sm:text-3xl lg:text-4xl font-extrabold text-fg tracking-tight break-words">Contract Management</h1>
            <p className="text-base sm:text-lg text-fg-subtle mt-1 break-words">Order #ORD-{contract.id + 8000} • {contract.project_title}</p>
          </div>
          <Link to={`/my-projects/${contract.project_id}`} className="group inline-flex items-center justify-center gap-2 px-6 py-3 bg-surface border border-line rounded-lg text-sm font-semibold text-fg-muted hover:bg-surface-2 transition-all shadow-sm shrink-0">
            View Project Brief <ExternalLink className="w-4 h-4" />
          </Link>
        </div>

{(contract.status === 'completed' || contract.status === 'refunded') && (
  <div className="mb-10 grid grid-cols-1 lg:grid-cols-2 gap-8">
    
    <div className="bg-surface border border-line rounded-xl shadow-sm overflow-hidden">
      <div className="bg-surface-2 px-6 py-4 border-b border-line flex items-center gap-3">
        <Star className="w-4 h-4 text-warning-soft-fg fill-amber-500" />
        <h3 className="text-xs font-bold text-fg uppercase tracking-widest">Your Review</h3>
      </div>
      <div className="p-6">
        {existingReview ? (
          <div className="space-y-4">
            <div className="flex items-center gap-1">
              {[...Array(5)].map((_, i) => (
                <Star key={i} className={`w-4 h-4 ${i < existingReview.rating ? 'text-warning-soft-fg fill-amber-500' : 'text-fg-subtle fill-slate-200'}`} />
              ))}
            </div>
            <p className="text-sm text-fg-muted font-medium italic leading-relaxed">"{existingReview.comment}"</p>
          </div>
        ) : (
          <div className="space-y-4">
            <div className="flex items-center gap-2 mb-4">
              {[1, 2, 3, 4, 5].map((num) => (
                <button key={num} onClick={() => setRating(num)}>
                  <Star className={`w-6 h-6 ${num <= rating ? 'text-warning fill-amber-400' : 'text-fg-subtle'}`} />
                </button>
              ))}
            </div>
            <textarea 
              value={comment}
              onChange={(e) => setComment(e.target.value)}
              placeholder="Leave feedback for the freelancer..."
              className="w-full h-20 p-3 bg-surface-2 border border-line rounded-lg text-sm outline-none focus-visible:ring-1 focus-visible:ring-brand transition-all resize-none"
            />
            <button 
              onClick={handleSubmitReview}
              disabled={createReviewMutation.isPending}
              className="w-full py-2 bg-brand text-brand-fg text-[10px] font-bold rounded-md uppercase tracking-widest hover:bg-brand-hover transition-all"
            >
              {createReviewMutation.isPending ? "Posting..." : "Post Review"}
            </button>
          </div>
        )}
      </div>
    </div>

    <div className="bg-surface border border-line rounded-xl shadow-sm overflow-hidden">
      <div className="bg-surface-2 px-6 py-4 border-b border-line flex items-center gap-3">
        <User className="w-4 h-4 text-brand" />
        <h3 className="text-xs font-bold text-fg uppercase tracking-widest">Freelancer's Review</h3>
      </div>
      <div className="p-6">
        {freelancerReview ? (
          <div className="space-y-4">
            <div className="flex items-center gap-1">
              {[...Array(5)].map((_, i) => (
                <Star key={i} className={`w-4 h-4 ${i < freelancerReview.rating ? 'text-brand fill-blue-500' : 'text-fg-subtle fill-slate-200'}`} />
              ))}
              <span className="ml-2 text-[10px] font-bold text-fg-subtle uppercase">Received Feedback</span>
            </div>
            <p className="text-sm text-fg-muted font-medium italic leading-relaxed">"{freelancerReview.comment}"</p>
          </div>
        ) : (
          <div className="py-8 flex flex-col items-center justify-center text-center">
            <History className="w-8 h-8 text-fg-subtle mb-2" />
            <p className="text-xs text-fg-subtle font-bold uppercase tracking-widest">Awaiting Feedback</p>
            <p className="text-[10px] text-fg-subtle mt-1 uppercase">The freelancer has not rated this experience yet.</p>
          </div>
        )}
      </div>
    </div>

  </div>
)}
        {dispute && (
          <div className="mb-10 bg-surface border border-line rounded-xl shadow-sm overflow-hidden">
            <div className="bg-surface-2 px-4 sm:px-8 py-5 border-b border-line flex flex-col sm:flex-row sm:items-center justify-between gap-4">
              <div className="flex items-center gap-4">
                <div className="p-2.5 bg-warning-soft rounded-lg text-warning shrink-0">
                  <Scale className="w-5 h-5" />
                </div>
                <div className="break-words overflow-hidden">
                  <div className="flex flex-wrap items-center gap-2">
                    <h3 className="text-base font-bold text-fg">Resolution Center Audit</h3>
                    <span className={`text-[10px] px-2 py-0.5 rounded border font-bold uppercase ${isFreelancerDisputed ? 'bg-warning-soft text-warning-soft-fg border-warning/30' : 'bg-brand-soft text-brand border-brand-line'}`}>
                      {isFreelancerDisputed ? "Raised by Freelancer" : "Raised by You"}
                    </span>
                  </div>
                  <p className="text-xs text-fg-subtle font-medium">Case ID: #{dispute.id}</p>
                </div>
              </div>
              <span className={`w-fit px-4 py-1.5 rounded-md text-xs font-bold uppercase tracking-wider border ${dispute.status === 'rejected' ? 'bg-danger-soft text-danger border-danger/30' : 'bg-warning-soft text-warning border-warning/30'
                }`}>
                {dispute.status}
              </span>
            </div>
            <div className="p-4 sm:p-8 grid grid-cols-1 md:grid-cols-2 gap-10">
              <div className="space-y-4 break-words overflow-hidden">
                <div className="flex items-center gap-2 text-fg-subtle">
                  <User className="w-4 h-4" />
                  <h4 className="text-xs font-bold uppercase tracking-widest">Dispute Statement</h4>
                </div>
                <div className="p-5 bg-surface-2 border border-line-soft rounded-lg">
                  <p className="text-sm font-semibold text-fg leading-relaxed break-words">{dispute.reason}</p>
                  <p className="text-[11px] text-fg-subtle mt-3 font-bold uppercase italic">Log Date: {new Date(dispute.created_at).toLocaleDateString()}</p>
                </div>
              </div>
              <div className="space-y-4 md:border-l border-line-soft md:pl-10 break-words overflow-hidden">
                <div className="flex items-center gap-2 text-fg-subtle">
                  <Landmark className="w-4 h-4" />
                  <h4 className="text-xs font-bold uppercase tracking-widest">Arbitration Notes</h4>
                </div>
                <p className="text-sm text-fg-muted leading-relaxed font-medium break-words">
                  {dispute.admin_notes || "Determination pending administrative review. Both parties will be notified upon a final decision."}
                </p>
                {dispute.resolved_at && (
                  <div className="flex items-center gap-2 mt-4 text-success">
                    <CheckCircle2 className="w-4 h-4" />
                    <span className="text-xs font-bold uppercase">Resolved on {new Date(dispute.resolved_at).toLocaleDateString()}</span>
                  </div>
                )}
              </div>
            </div>
          </div>
        )}

        <div className="bg-surface rounded-xl border border-line shadow-sm overflow-hidden mb-10">
          <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-5 divide-y lg:divide-y-0 lg:divide-x divide-slate-100">
            <div className="p-6 sm:p-8">
              <p className="text-[11px] font-bold text-fg-subtle uppercase tracking-widest mb-2">Reference</p>
              <p className="text-xl font-bold text-fg break-all">#ORD-{contract.id + 8000}</p>
            </div>
            <div className="p-6 sm:p-8">
              <p className="text-[11px] font-bold text-fg-subtle uppercase tracking-widest mb-2">Vendor</p>
              <p className="text-xl font-bold text-fg break-words">{contract.freelancer_name}</p>
            </div>
            <div className="p-6 sm:p-8">
              <p className="text-[11px] font-bold text-fg-subtle uppercase tracking-widest mb-2">Service Tier</p>
              <p className="text-xl font-bold text-brand break-words">{contract.package_name || "Enterprise"}</p>
            </div>
            <div className="p-6 sm:p-8">
              <p className="text-[11px] font-bold text-fg-subtle uppercase tracking-widest mb-2">Contract Status</p>
              <div className="flex items-center gap-2">
                <span className={`w-2 h-2 rounded-full shrink-0 ${contract.status === 'completed' ? 'bg-success-soft' : 'bg-warning-soft'}`} />
                <p className="text-xl font-bold text-fg uppercase break-all">{contract.status}</p>
              </div>
            </div>
            <div className="p-6 sm:p-8 bg-surface-2/50">
              <p className="text-[11px] font-bold text-fg-subtle uppercase tracking-widest mb-2">Payment Status</p>
              <div className="flex items-center gap-2">
                <span className={`w-2 h-2 rounded-full shrink-0 ${contract.payment_status === 'released' ? 'bg-success-soft' :
                    contract.payment_status === 'refunded' ? 'bg-rose-500' : 'bg-brand-soft'
                  }`} />
                <p className={`text-xl font-bold uppercase break-words ${contract.payment_status === 'released' ? 'text-success' :
                    contract.payment_status === 'refunded' ? 'text-rose-700' : 'text-brand'
                  }`}>
                  {contract.payment_status?.replace('_', ' ') || "Pending"}
                </p>
              </div>
            </div>
          </div>
        </div>

        <div className="space-y-4 mb-10">
          {contract.revisions?.some(r => r.status === 'rejected') && (
            <div className="p-5 bg-danger-soft border border-danger/30 rounded-lg flex gap-4 break-words overflow-hidden">
              <AlertCircle className="w-6 h-6 text-danger shrink-0" />
              <div>
                <p className="text-sm font-bold text-danger-soft-fg">Modification Request Declined</p>
                <p className="text-xs text-danger mt-1 font-medium break-words">{contract.revisions.find(r => r.status === 'rejected')?.rejection_message}</p>
              </div>
            </div>
          )}
          {acceptedRevision && (
            <div className="p-5 bg-success-soft border border-success/30 rounded-lg flex gap-4 break-words overflow-hidden">
              <CheckCircle2 className="w-6 h-6 text-success shrink-0" />
              <div>
                <p className="text-sm font-bold text-success-soft-fg">Revision Request Accepted</p>
                <p className="text-xs text-success mt-1 font-medium break-words">The vendor has accepted the modifications and is processing the update.</p>
              </div>
            </div>
          )}
        </div>

        <div className="grid grid-cols-1 lg:grid-cols-12 gap-10">
          <div className="lg:col-span-8 space-y-10 min-w-0">
            <div className="bg-surface rounded-xl border border-line shadow-sm p-6 sm:p-10 break-words overflow-hidden">
              <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 mb-8 border-b border-line-soft pb-6">
                <h2 className="text-lg sm:text-xl font-bold text-fg flex items-center gap-3 uppercase tracking-tight">
                  <FileText className="w-6 h-6 text-fg-subtle shrink-0" />
                  Service Specifications
                </h2>
                <div className="w-fit flex items-center gap-2 px-3 py-1 bg-surface-3 rounded-full text-fg-muted">
                  <Tag className="w-3.5 h-3.5 shrink-0" />
                  <span className="text-[10px] font-bold uppercase truncate max-w-[150px]">{contract.project_category}</span>
                </div>
              </div>
              <div className="space-y-8">
                <div>
                  <h3 className="text-lg sm:text-xl font-bold text-fg break-words">{contract.project_title}</h3>
                </div>

                <div className="space-y-4 break-words">
                  <div className="flex items-center gap-2 text-fg-subtle">
                    <Info className="w-4 h-4 shrink-0" />
                    <h4 className="text-xs font-bold uppercase tracking-widest">Description</h4>
                  </div>
                  <div className="p-6 sm:p-8 bg-surface-2 border border-line-soft rounded-lg text-fg-muted leading-relaxed whitespace-pre-line text-sm sm:text-base break-words overflow-hidden">
                    {contract.project_description}
                  </div>
                </div>

                <div className="grid grid-cols-1 md:grid-cols-2 gap-8">
                  <div className="space-y-4 break-words overflow-hidden">
                    <div className="flex items-center gap-2 text-fg-subtle">
                      <ListChecks className="w-4 h-4 shrink-0" />
                      <h4 className="text-xs font-bold uppercase tracking-widest">Requirements</h4>
                    </div>
                    <div className="p-6 bg-surface-2/50 border border-line-soft rounded-lg text-sm text-fg-muted leading-relaxed whitespace-pre-line break-words">
                      {contract.requirements || "No specific requirements provided."}
                    </div>
                  </div>
                  <div className="space-y-4 break-words overflow-hidden">
                    <div className="flex items-center gap-2 text-fg-subtle">
                      <Target className="w-4 h-4 shrink-0" />
                      <h4 className="text-xs font-bold uppercase tracking-widest">Expected Deliverables</h4>
                    </div>
                    <div className="p-6 bg-surface-2/50 border border-line-soft rounded-lg text-sm text-fg-muted leading-relaxed whitespace-pre-line break-words">
                      {contract.expected_deliverables || "No specific deliverables stated."}
                    </div>
                  </div>
                </div>

                <div className="space-y-4 break-words overflow-hidden">
                  <div className="flex items-center gap-2 text-fg-subtle">
                    <Layers className="w-4 h-4 shrink-0" />
                    <h4 className="text-xs font-bold uppercase tracking-widest">Required Technologies</h4>
                  </div>
                  <div className="flex flex-wrap gap-2">
                    {contract.skills?.map((skill, i) => (
                      <span key={i} className="px-4 py-2 bg-surface border border-line rounded-lg text-xs font-bold text-fg-muted shadow-sm break-all max-w-full">
                        {skill}
                      </span>
                    ))}
                  </div>
                </div>
              </div>

              <div className="grid grid-cols-1 sm:grid-cols-3 gap-8 mt-12 pt-8 border-t border-line-soft">
                <div>
                  <p className="text-[11px] text-fg-subtle font-bold uppercase tracking-widest">Effective Date</p>
                  <p className="text-base font-bold text-fg-muted mt-1">{new Date(contract.freelancer_signed_at).toLocaleDateString()}</p>
                </div>
                <div>
                  <p className="text-[11px] text-fg-subtle font-bold uppercase tracking-widest">Target Completion</p>
                  <p className="text-base font-bold text-fg-muted mt-1">{dueDate.toLocaleDateString()}</p>
                </div>
                <div>
                  <p className="text-[11px] text-fg-subtle font-bold uppercase tracking-widest">Countdown</p>
                  <p className={`text-base font-bold mt-1 ${timeLeft.days < 2 ? 'text-danger' : 'text-brand'}`}>
                    {contract.status === 'completed' ? 'Delivered' : `${timeLeft.days}D : ${timeLeft.hours}H`}
                  </p>
                </div>
              </div>
            </div>

            <div className="bg-surface rounded-xl border border-line shadow-sm overflow-hidden min-w-0">
              <div className="px-6 sm:px-10 py-6 border-b border-line-soft flex flex-col sm:flex-row sm:justify-between sm:items-center gap-4 bg-surface-2/30">
                <h2 className="text-lg font-bold text-fg uppercase tracking-tight">Project Deliverables</h2>
                {contract.status === 'active' && (
                  <span className="w-fit flex items-center gap-2 text-[10px] bg-brand-soft text-brand px-4 py-1.5 rounded-md font-bold border border-brand-line uppercase tracking-widest">
                    <Clock className="w-3.5 h-3.5 shrink-0" /> Work in Progress
                  </span>
                )}
              </div>
              <div className="p-6 sm:p-10 break-words overflow-hidden">
                {contract.deliverables?.length > 0 ? (
                  <div className="space-y-4">
                    {contract.deliverables.map((item, index) => (
                      <div key={item.id} className="flex flex-col sm:flex-row sm:items-center justify-between p-6 bg-surface rounded-lg border border-line-soft hover:border-line transition-all shadow-sm gap-6">
                        <div className="flex items-center gap-5 break-words overflow-hidden">
                          <div className="p-4 bg-surface-2 text-fg-subtle rounded-lg border border-line-soft shrink-0">
                            <FileText className="w-6 h-6" />
                          </div>
                          <div className="break-words overflow-hidden">
                            <p className="text-base font-bold text-fg break-words">{item.title || `Asset Submission #${index + 1}`}</p>
                            {/* `submitted_at`, not `created_at`: DeliverableResponse
                                exposes SubmittedAt, and reading `created_at` here
                                rendered the literal string "Invalid Date" next to
                                every file. The type and the date are also joined
                                only when the date actually resolved. */}
                            <p className="text-xs text-fg-subtle font-bold uppercase tracking-widest mt-1 break-words">
                              {[item.deliverable_type, formatDateDMY(item.submitted_at, "—")].filter(Boolean).join(" • ")}
                            </p>
                          </div>
                        </div>
                        {/* A link-only deliverable has no file_url, so an
                            unconditional href produced a dead Download button. */}
                        {item.file_url ? (
                          <a href={item.file_url} target="_blank" rel="noopener noreferrer" className="flex items-center justify-center gap-2 px-6 py-3 bg-inverse text-white text-xs font-bold rounded-md hover:bg-inverse transition-colors shrink-0">
                            <Download className="w-4 h-4 shrink-0" /> Download
                          </a>
                        ) : (
                          <a href={item.link_url} target="_blank" rel="noopener noreferrer" className="flex items-center justify-center gap-2 px-6 py-3 bg-brand text-brand-fg text-xs font-bold rounded-md hover:bg-brand-hover transition-colors shrink-0">
                            <ExternalLink className="w-4 h-4 shrink-0" /> Open Link
                          </a>
                        )}
                      </div>
                    ))}

                    {/* The state machine allows Disputed -> Completed, but the
                        button only rendered on 'submitted', so a disputed contract
                        offered the client no way to mark it complete. */}
                    {['submitted', 'disputed'].includes(contract.status) && (
                      <div className="flex flex-col sm:flex-row gap-4 pt-10">
                        <button onClick={handleApproveOrder} disabled={updateStatusMutation.isPending} className="w-full sm:w-fit px-10 py-4 bg-success text-success-fg text-sm font-bold rounded-md hover:bg-success transition-all flex items-center justify-center gap-2 shadow-lg shadow-green-100">
                          <CheckCircle2 className="w-5 h-5 shrink-0" /> Approve Submissions
                        </button>
                        {pendingRevision ? (
                          <div className="w-full sm:w-fit px-8 py-4 bg-surface-2 text-fg-subtle border border-line rounded-md text-xs font-bold uppercase tracking-widest text-center">Revision Pending</div>
                        ) : (
                          <button onClick={() => setIsRevisionModalOpen(true)} className="w-full sm:w-fit px-10 py-4 bg-surface border border-line text-fg-muted text-sm font-bold rounded-md hover:bg-surface-2 transition-colors">
                            Request Modification
                          </button>
                        )}
                      </div>
                    )}
                  </div>
                ) : (
                  <div className="text-center py-20 bg-surface-2/30 rounded-lg border border-dashed border-line">
                    <History className="w-12 h-12 text-fg-subtle mx-auto mb-4 shrink-0" />
                    <p className="text-base font-bold text-fg">No deliverables found</p>
                    <p className="text-sm text-fg-subtle mt-1 max-w-xs mx-auto">Assets will appear here once submitted by the vendor.</p>
                  </div>
                )}
              </div>
            </div>
          </div>

          <div className="lg:col-span-4 space-y-8 min-w-0">
            <div className="bg-surface rounded-xl border border-line shadow-sm p-6 sm:p-10 break-words overflow-hidden">
              <h2 className="text-[11px] font-bold text-fg-subtle uppercase tracking-[0.2em] mb-10">Financial Overview</h2>
              <div className="space-y-6">
                <div className="flex justify-between text-sm font-semibold text-fg-subtle gap-4">
                  <span className="shrink-0">Agreement Subtotal</span>
                  <span className="text-fg break-all text-right">${Number(contract.total_amount).toLocaleString()}</span>
                </div>
                <div className="flex justify-between text-sm font-semibold text-fg-subtle gap-4">
                  <span className="shrink-0">Transaction Fees</span>
                  <span className="text-fg text-right">$0.00</span>
                </div>
                <div className="pt-8 border-t border-line-soft flex justify-between items-end gap-4">
                  <span className="text-xs font-bold text-fg uppercase tracking-widest shrink-0">Total Paid</span>
                  <span className="text-2xl sm:text-3xl font-black text-fg tracking-tighter break-all text-right">${(contract.total_amount).toLocaleString()}</span>
                </div>
              </div>
              <div className="mt-10 p-4 bg-brand-soft/50 rounded-lg border border-brand-line flex items-start gap-3">
                <ShieldCheck className="w-5 h-5 text-brand mt-0.5 shrink-0" />
                <p className="text-xs text-brand font-semibold leading-relaxed break-words">Funds are held securely by our platform and disbursed only upon deliverable approval.</p>
              </div>
            </div>

            {contract.legal_document_url && (
              <div className="bg-surface rounded-xl border border-line shadow-sm p-6 sm:p-8 hover:border-brand-line transition-colors group break-words overflow-hidden">
                <div className="flex items-center gap-5 mb-8 break-words overflow-hidden">
                  <div className="p-3 bg-surface-2 text-fg-subtle rounded-lg group-hover:text-brand group-hover:bg-brand-hover-soft transition-colors shrink-0">
                    <Landmark className="w-6 h-6" />
                  </div>
                  <div className="break-words overflow-hidden">
                    <h3 className="text-xs font-bold text-fg uppercase tracking-widest break-words">Service Agreement</h3>
                    <p className="text-[10px] text-fg-subtle font-bold break-words">LEGALLY BINDING DOCUMENT</p>
                  </div>
                </div>
                <a href={contract.legal_document_url} target="_blank" rel="noopener noreferrer" className="w-full flex items-center justify-center gap-3 py-4 bg-surface-2 text-fg-muted text-xs font-bold rounded-lg hover:bg-surface-3 border border-line-soft uppercase tracking-widest transition-all text-center">
                  <Download className="w-4 h-4 shrink-0" /> Download Agreement
                </a>
              </div>
            )}

            {!dispute?.id && (
              <div className="grid grid-cols-1 gap-4">
                {contract?.status !== 'completed' &&
                  <button onClick={handleMessageFreelancer} disabled={getRoomMutation.isPending} className="w-full flex items-center justify-center gap-4 py-5 bg-surface border border-line text-fg text-sm font-bold rounded-lg hover:bg-surface-2 shadow-sm uppercase tracking-widest transition-all px-4 text-center break-words">
                    <MessageSquare className="w-5 h-5 text-fg-subtle shrink-0" /> {getRoomMutation.isPending ? "Connecting..." : "Open Workspace Chat"}
                  </button>
                }

                {contract.status === 'disputed' || contract.status === 'dispute' ? (
                  <div className="w-full flex items-center justify-center gap-4 py-5 bg-surface-2 border border-line text-fg-subtle text-xs font-bold rounded-lg uppercase tracking-widest px-4 text-center break-words">
                    <ShieldAlert className="w-5 h-5 shrink-0" /> Dispute Under Review
                  </div>
                ) : (
                  <button onClick={() => openModal("client-raise-dispute", { contractId: contract.id })} className="w-full flex items-center justify-center gap-4 py-5 bg-surface border border-red-50 text-danger text-[10px] font-bold rounded-lg hover:bg-danger-soft transition-all uppercase tracking-widest px-4 text-center break-words">
                    <ShieldAlert className="w-4 h-4 shrink-0" /> Raise Dispute
                  </button>
                )}
              </div>
            )}
          </div>
        </div>
      </div>

      {/* Hand-rolled scrim: no Escape (WCAG 2.1.2), no focus trap, no focus
          restoration, no role="dialog". The textarea also had no label element
          at all -- not a broken one, none -- so it announced as a bare "edit
          text" box. */}
      <Dialog
        open={isRevisionModalOpen}
        onClose={() => setIsRevisionModalOpen(false)}
        title="Request Modification"
        description={`Order #ORD-${contract.id + 8000}`}
        size="lg"
        className="p-0 overflow-hidden flex flex-col"
      >
        <div className="px-6 sm:px-10 py-6 sm:py-8 border-b border-line-soft flex justify-between items-start shrink-0">
          <div className="break-words overflow-hidden pr-4">
            <h3 className="text-lg sm:text-xl font-bold text-fg tracking-tight break-words">Request Modification</h3>
            <p className="text-xs text-fg-subtle font-bold uppercase tracking-widest mt-1 break-words">Order #ORD-{contract.id + 8000}</p>
          </div>
          <DialogClose className="shrink-0">
            <X className="w-6 h-6" aria-hidden="true" />
          </DialogClose>
        </div>
        <div className="p-6 sm:p-10 overflow-y-auto">
          <div className="mb-6 p-5 bg-brand-soft border border-brand-line rounded-lg flex gap-4 break-words overflow-hidden shrink-0">
            <Info className="w-5 h-5 text-brand shrink-0" aria-hidden="true" />
            <p className="text-xs text-brand font-semibold leading-relaxed break-words">Provide specific feedback regarding the changes required to meet the original brief.</p>
          </div>
          <Field
            label="What needs to change?"
            hint="Be specific. The freelancer sees this verbatim."
          >
            {(f) => (
              <textarea
                {...f}
                className="w-full h-40 p-5 bg-surface-2 border border-line rounded-lg text-sm focus-visible:ring-2 focus-visible:ring-brand/20 focus-visible:bg-surface outline-none transition-all resize-none font-medium break-words"
                placeholder="State the specific deliverables that need adjustment..."
                value={revisionReason}
                onChange={(e) => setRevisionReason(e.target.value)}
              />
            )}
          </Field>
        </div>
        <div className="px-6 sm:px-10 py-6 sm:py-8 bg-surface-2 flex flex-col sm:flex-row gap-4 shrink-0">
          <button onClick={() => setIsRevisionModalOpen(false)} className="w-full sm:flex-1 px-6 py-4 bg-surface border border-line text-fg-muted text-xs font-bold rounded-lg uppercase tracking-widest hover:bg-surface-3 transition-colors">Cancel</button>
          <button onClick={handleRevisionSubmit} disabled={requestRevisionMutation.isPending || revisionReason.length < 10} className="w-full sm:flex-1 px-6 py-4 bg-danger text-danger-fg text-xs font-bold rounded-lg uppercase tracking-widest hover:bg-danger transition-all disabled:opacity-50">
            {requestRevisionMutation.isPending ? "Submitting..." : "Submit Request"}
          </button>
        </div>
      </Dialog>
    </div>
  );
}