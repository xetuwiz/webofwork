import { useState } from "react";
import Tooltip from '../../../components/common/Tooltip';
import { useParams, useNavigate } from "react-router-dom";
import {
  X,
  FileText,
  Download,
  AlertCircle,
  Loader2,
  ShieldCheck,
  Scale,
  CheckCircle2,
  Lock,
  User,
  Briefcase,
  Paperclip,
  Info,
  Calendar,
  AlertTriangle,
  ExternalLink
} from "lucide-react";
import { useForm } from "react-hook-form";
import { useAdminDisputeDetail } from "./disputeQueries";
import { useResolveDispute } from "./disputeMutations";
import Dialog from "../../../components/common/Dialog";

export default function DisputeDetail() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [confirmModal, setConfirmModal] = useState({ show: false, action: null });

  const { data: dispute, isLoading, isError } = useAdminDisputeDetail(id);
  const mutation = useResolveDispute();

  const {
    register,
    getValues,
    trigger,
    formState: { errors }
  } = useForm({
    defaultValues: {
      adminNote: ""
    }
  });

  if (isLoading) return (
    <div className="min-h-screen flex flex-col items-center justify-center bg-surface-2">
      <Loader2 className="w-8 h-8 animate-spin text-fg-subtle mb-4" />
      <p className="text-fg-subtle text-sm font-medium">Fetching Audit Evidence...</p>
    </div>
  );

  if (isError || !dispute) return (
    <div className="min-h-screen bg-surface flex items-center justify-center p-6 text-center">
      <div>
        <AlertCircle className="w-12 h-12 text-danger-soft-fg mx-auto mb-4" />
        <h2 className="text-xl font-bold">Case Record Unavailable</h2>
        <button onClick={() => navigate("/admin/disputes")} className="mt-4 text-brand font-bold">Return to Dashboard</button>
      </div>
    </div>
  );

  const contract = dispute.contract_details;
  const isPending = dispute.status === 'pending';
  const opener = dispute.opened_by_client ? 'client' : 'freelancer';

  const handleDecision = () => {
    const action = confirmModal.action;
    let finalDisputeStatus;

    if (action === 'APPROVE_OPENER') {
      finalDisputeStatus = 'resolved';
    } else {
      finalDisputeStatus = 'rejected';
    }

    mutation.mutate({
      id,
      status: finalDisputeStatus,
      admin_notes: getValues("adminNote"),
    }, {
      onSuccess: () => setConfirmModal({ show: false, action: null })
    });
  };

  const openConfirmModal = async (action) => {
    const isValid = await trigger("adminNote");
    if (!isValid) return;
    setConfirmModal({ show: true, action });
  };

  return (
    <div className="min-h-screen bg-surface text-fg font-sans">
      {/* Hand-rolled scrim. This one matters more than most: it is a
          confirmation for a *legally binding, irreversible* arbitration
          decision, and it had no Escape handling, no focus trap, and no
          role="dialog". A keyboard user who opened it could tab straight out
          into the dispute record behind it.
          `open` is derived from state rather than gated on `show &&`, since
          <Dialog> owns its own visibility -- the old `&&` is folded into it. */}
      <Dialog
        open={confirmModal.show}
        onClose={() => setConfirmModal({ show: false, action: null })}
        title="Confirm Determination"
        description="This action is permanent and legally binding within the platform."
        size="md"
        className="p-0 overflow-hidden"
      >
        <div className="p-6">
          <div className={`w-12 h-12 rounded-full flex items-center justify-center mb-4 ${confirmModal.action === 'APPROVE_OPENER' ? 'bg-brand-soft' : 'bg-danger-soft'}`}>
            <AlertTriangle className={`w-6 h-6 ${confirmModal.action === 'APPROVE_OPENER' ? 'text-brand' : 'text-danger'}`} aria-hidden="true" />
          </div>
          <h3 className="text-lg font-bold text-fg mb-2">Confirm Determination</h3>
          <p className="text-sm text-fg-subtle leading-relaxed">
            You are about to {confirmModal.action === 'APPROVE_OPENER' ? <span className="font-bold text-brand">resolve this dispute in favor of the {opener}</span> : <span className="font-bold text-danger">reject this claim</span>}. This action is permanent and legally binding within the platform.
          </p>
          <div className="mt-4 p-3 bg-surface-2 rounded-lg border border-line-soft">
            <p className="text-[10px] font-bold text-fg-subtle uppercase mb-1">Final Note:</p>
            <p className="text-xs text-fg-muted italic">"{getValues("adminNote")}"</p>
          </div>
        </div>
        <div className="flex items-center gap-3 p-6 bg-surface-2 border-t border-line-soft">
          <button
            onClick={() => setConfirmModal({ show: false, action: null })}
            className="flex-1 px-4 py-2.5 text-xs font-bold text-fg-muted hover:bg-surface-inset rounded-xl transition-colors"
          >
            Go Back
          </button>
          <button
            onClick={handleDecision}
            disabled={mutation.isPending}
            className={`flex-1 px-4 py-2.5 text-xs font-bold text-white rounded-xl transition-all shadow-sm ${confirmModal.action === 'APPROVE_OPENER' ? 'bg-brand hover:bg-brand-hover' : 'bg-danger hover:bg-danger'}`}
          >
            {mutation.isPending ? "Processing..." : "Confirm & Finalize"}
          </button>
        </div>
      </Dialog>

      <header className="bg-surface border-b border-line sticky top-0 z-20">
        <div className="max-w-page-2xl mx-auto px-6 h-16 flex items-center justify-between">
          <div className="flex items-center gap-4">
            <button onClick={() => navigate("/admin/disputes")} className="p-1.5 hover:bg-surface-3 rounded transition-colors border border-transparent hover:border-line">
              <X className="w-4 h-4 text-fg-subtle" />
            </button>
            <div className="flex flex-col">
              <div className="flex items-center gap-2">
                <span className="text-[10px] font-bold text-fg-subtle uppercase tracking-wider">Arbitration Case</span>
                <span className="text-xs font-bold text-fg">#DSP-{dispute.id}</span>
              </div>
              <h1 className="text-sm font-semibold text-fg-muted">Dispute Review Portal</h1>
            </div>
          </div>

          <div className={`px-3 py-1 rounded-full text-[10px] font-bold border uppercase tracking-widest ${isPending ? 'bg-warning-soft text-warning border-warning/30' : 'bg-surface-3 text-fg-muted border-line'
            }`}>
            {dispute.status.replace('_', ' ')}
          </div>
        </div>
      </header>

      <div className="max-w-page-2xl mx-auto p-6 grid grid-cols-12 gap-8">
        <div className="col-span-12 lg:col-span-8 space-y-6">

          <div className="flex items-center gap-3 p-4 bg-surface border border-line rounded-xl shadow-sm">
            <div className="w-10 h-10 bg-surface-3 rounded-full flex items-center justify-center">
              {opener === 'client' ? <User className="w-5 h-5 text-fg-muted" /> : <Briefcase className="w-5 h-5 text-fg-muted" />}
            </div>
            <div>
              <p className="text-[10px] font-bold text-fg-subtle uppercase tracking-tight">Dispute Raised By</p>
              <p className="text-sm font-bold text-fg">{opener === 'client' ? 'Client' : 'Freelancer'} — <span className="font-medium text-fg-subtle">{opener === 'client' ? dispute.client_name : dispute.freelancer_name}</span></p>
            </div>
          </div>

          <section className="bg-surface border border-line rounded-xl shadow-sm overflow-hidden">
            <div className="px-6 py-4 border-b border-line-soft bg-surface-2/50 flex items-center justify-between">
              <h2 className="text-xs font-bold text-fg-subtle uppercase tracking-widest flex items-center gap-2">
                <Scale className="w-3.5 h-3.5" /> Filed Claim
              </h2>
              <span className="text-[10px] text-fg-subtle font-medium">Date: {new Date(dispute.created_at).toLocaleDateString()}</span>
            </div>
            <div className="p-6">
              <h3 className="text-lg font-bold text-fg mb-3 capitalize">{dispute.reason.replace(/_/g, ' ')}</h3>
              <div className="p-5 bg-surface-2 border border-line-soft rounded-xl mb-8">
                <p className="text-sm text-fg-muted leading-relaxed italic">"{dispute.description}"</p>
              </div>

              <h4 className="text-[10px] font-bold text-fg-subtle uppercase tracking-widest mb-4">Evidence Gallery</h4>
              <div className="grid grid-cols-2 gap-4">
                {dispute.evidence_files?.map((file, index) => (
                  <div key={index} className="flex items-center justify-between p-3 border border-line rounded-lg hover:border-primary transition-all group">
                    <div className="flex items-center gap-3 min-w-0">
                      <FileText className="w-4 h-4 text-fg-subtle group-hover:text-brand" />
                      <span className="text-xs font-semibold text-fg-muted truncate">Exhibit_{index + 1}</span>
                    </div>
                    <a href={file} target="_blank" rel="noreferrer" className="text-fg-subtle hover:text-brand">
                      <Download className="w-4 h-4" />
                    </a>
                  </div>
                ))}
              </div>
            </div>
          </section>

          <section className="bg-surface border border-line rounded-xl shadow-sm overflow-hidden border-t-4 border-t-primary/20">
            <div className="px-6 py-4 border-b border-line-soft bg-surface-2/50 flex items-center justify-between">
              <h2 className="text-xs font-bold text-fg-subtle uppercase tracking-widest">Administrative Determination</h2>
              {!isPending && <Lock className="w-3.5 h-3.5 text-fg-subtle" />}
            </div>

            <div className="p-6">
              {isPending ? (
                <>
                  <textarea
                    {...register("adminNote", { required: "Internal review notes are required for the audit trail." })}
                    placeholder="Provide a detailed rationale for this decision..."
                    className={`w-full h-36 p-4 bg-surface-2 border rounded-xl text-sm outline-none mb-1 focus-visible:bg-surface focus-visible:ring-2 focus-visible:ring-primary/5 transition-all resize-none ${errors.adminNote ? 'border-red-500' : 'border-line'}`}
                  />
                  {errors.adminNote && (
                    <p className="text-[10px] font-bold text-danger mb-6 flex items-center gap-1">
                      <AlertCircle className="w-3 h-3" /> {errors.adminNote.message}
                    </p>
                  )}
                  <div className={`grid grid-cols-2 gap-4 ${!errors.adminNote ? 'mt-6' : ''}`}>
                    <button
                      onClick={() => openConfirmModal('APPROVE_OPENER')}
                      disabled={mutation.isPending}
                      className="bg-surface hover:bg-brand-hover  hover:text-brand-fg text-brand border-2 border-primary font-bold text-[11px] py-4 rounded-xl uppercase tracking-widest hover:opacity-90 disabled:bg-surface-inset transition-all shadow-md shadow-primary/10"
                    >
                      Resolve in Favor of {opener}
                    </button>
                    <button
                      onClick={() => openConfirmModal('REJECT_OPENER')}
                      disabled={mutation.isPending}
                      className="bg-surface border-2 border-red-700 text-danger font-bold text-[11px] py-4 rounded-xl uppercase tracking-widest hover:bg-danger hover:text-danger-fg  transition-all"
                    >
                      Reject Claim
                    </button>
                  </div>
                  <p className="text-[10px] text-fg-subtle mt-4 text-center italic">
                    Note: Resolving in favor of {opener} will set contract to <strong>{opener === 'client' ? 'Refunded' : 'Completed'}</strong>.
                  </p>
                </>
              ) : (
                <div className="py-4 text-center">
                  <div className="w-12 h-12 bg-emerald-50 rounded-full flex items-center justify-center mx-auto mb-4">
                    <CheckCircle2 className="w-6 h-6 text-emerald-500" />
                  </div>
                  <h3 className="text-sm font-bold text-fg">Decision Archived</h3>
                  <div className="mt-4 text-left p-4 bg-surface-2 rounded-lg border border-line-soft">
                    <p className="text-xs text-fg-subtle uppercase font-bold mb-1">Internal Note:</p>
                    <p className="text-sm text-fg-muted leading-relaxed italic">"{dispute.admin_notes || "No notes provided."}"</p>
                  </div>
                </div>
              )}
            </div>
          </section>
        </div>

        <div className="col-span-12 lg:col-span-4 space-y-6">
          <aside className="bg-surface border border-line rounded-xl shadow-sm p-6">
            <h2 className="text-xs font-bold text-fg-subtle uppercase tracking-widest mb-6">Contract & Project Registry</h2>

            <div className="p-4 bg-surface-2 rounded-xl mb-6 border border-line-soft">
              <span className="text-[10px] font-bold text-fg-subtle uppercase">Escrow Amount</span>
              <div className="text-2xl font-black text-fg">${contract?.total_amount}</div>
            </div>

            <div className="space-y-6 px-1">
              <div className="flex flex-col">
                <span className="text-[10px] font-bold text-fg-subtle uppercase flex items-center gap-1.5 mb-1.5">
                  <Briefcase className="w-3 h-3" /> Project Title
                </span>
                <span className="text-sm font-semibold wrap-break-word text-fg-muted">{contract?.project_title}</span>
              </div>

              <div className="flex flex-col">
                <span className="text-[10px] font-bold text-fg-subtle uppercase flex items-center gap-1.5 mb-1.5">
                  <Info className="w-3 h-3" /> Description
                </span>
                <span className="text-[11px] leading-relaxed wrap-break-word text-fg-muted line-clamp-4 bg-surface p-2 rounded border border-line-soft">
                  {contract?.project_description}
                </span>
              </div>

              <div className="grid grid-cols-2 gap-4">
                <div className="flex flex-col">
                  <span className="text-[10px] font-bold text-fg-subtle uppercase mb-1">Category</span>
                  <span className="text-[11px] font-bold text-fg-muted px-2 py-1 bg-surface-3 rounded w-fit capitalize">
                    {contract?.project_category}
                  </span>
                </div>
                <div className="flex flex-col">
                  <span className="text-[10px] font-bold text-fg-subtle uppercase mb-1">Duration</span>
                  <div className="flex items-center gap-1 text-[11px] font-bold text-fg-muted">
                    <Calendar className="w-3 h-3" /> {contract?.delivery_days} Days
                  </div>
                </div>
              </div>

              <div className="flex flex-col">
                <span className="text-[10px] font-bold text-fg-subtle uppercase mb-1.5">Skills Required</span>
                <div className="flex flex-wrap gap-1">
                  {contract?.skills?.map((skill, i) => (
                    <span key={i} className="text-[9px] font-bold bg-surface border border-line px-1.5 py-0.5 rounded text-fg-subtle">
                      {skill}
                    </span>
                  ))}
                </div>
              </div>

              <div className="pt-4 border-t border-line-soft">
                <span className="text-[10px] font-bold text-fg-subtle uppercase tracking-widest block mb-3">Project Deliverables</span>
                <div className="space-y-2">
                  {contract?.deliverables?.length > 0 ? (
                    contract.deliverables.map((item) => (
                      <div key={item.id} className="flex items-center justify-between p-2.5 bg-surface border border-line rounded-lg group hover:border-primary transition-colors">
                        <div className="flex items-center gap-2 min-w-0">
                          <Paperclip className="w-3 h-3 text-fg-subtle group-hover:text-brand" />
                          <span className="text-[11px] font-medium text-fg-muted truncate">{item.title}</span>
                        </div>
                        {/* A link-only deliverable has no `file_url`, so this used to
                            render `href={undefined}` -- a dead control that reloaded
                            the page on click. An admin reviewing a dispute needs to
                            actually be able to open what was submitted. */}
                        {item?.file_url || item?.link_url ? (
                          // The aria-label says what the control DOES ("Download
                          // deliverable"); the tooltip says WHICH deliverable,
                          // which is the part a reviewer scanning the list
                          // actually needs. That was a `title=` attribute, so it
                          // only appeared on hover -- unreachable by keyboard.
                          <Tooltip content={item.title} side="top">
                            <a
                              href={item.file_url || item.link_url}
                              target="_blank"
                              rel="noopener noreferrer"
                              aria-label={`${item.file_url ? "Download deliverable" : "Open deliverable link"}: ${item.title}`}
                              className="p-1 hover:bg-surface-3 rounded inline-flex"
                            >
                              {item.file_url
                                ? <Download className="w-3 h-3 text-fg-subtle hover:text-brand" aria-hidden="true" />
                                : <ExternalLink className="w-3 h-3 text-fg-subtle hover:text-brand" aria-hidden="true" />}
                            </a>
                          </Tooltip>
                        ) : (
                          // Left as a native `title` on purpose. The span is
                          // `aria-hidden`, so wrapping it in <Tooltip> would
                          // produce a trigger assistive tech cannot see -- worse
                          // than what is here. It is decorative: the absence of
                          // a link is already the information, and the
                          // surrounding comment explains it.
                          <span title="No file or link was submitted" className="p-1 text-fg-subtle" aria-hidden="true">
                            <Paperclip className="w-3 h-3" />
                          </span>
                        )}
                      </div>
                    ))
                  ) : (
                    <p className="text-[10px] text-fg-subtle italic">No files submitted yet.</p>
                  )}
                </div>
              </div>

              <div className="pt-4 border-t border-line-soft">
                <span className="text-[10px] font-bold text-fg-subtle uppercase mb-2 block tracking-widest">Expected Deliverables List</span>
                <p className="text-[11px] text-fg-muted leading-relaxed wrap-break-word whitespace-pre-wrap bg-surface-2 p-2.5 rounded border border-line-soft">
                  {contract?.expected_deliverables}
                </p>
              </div>

              <div className="pt-4 border-t border-line-soft">
                <span className="text-[10px] font-bold text-fg-subtle uppercase">Current Contract Status</span>
                <div className="mt-1">
                  <span className="px-2 py-0.5 bg-inverse text-white text-[9px] font-bold rounded uppercase">
                    {contract?.status}
                  </span>
                </div>
              </div>
            </div>
          </aside>

          <div className="p-5 bg-surface-2 border-l-4 border-line-strong rounded-r-xl flex items-start gap-4 shadow-sm">
            <ShieldCheck className="w-5 h-5 text-fg-subtle shrink-0" />
            <div className="text-[11px] text-fg-muted leading-relaxed">
              <span className="text-fg font-bold block mb-1 uppercase tracking-tighter">Audit Compliance</span>
              Decisions are legally binding and immutable once finalized. All actions are logged with the administrator's ID and IP address.
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
