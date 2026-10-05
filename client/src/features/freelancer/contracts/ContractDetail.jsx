import React, { useState, useEffect } from "react";
import { useParams } from "react-router-dom";
import { useContractDetail } from "./contractsQueries";
import {
  FileText, Package, Clock, Calendar, Briefcase, Tag,
  Upload, MessageSquare, ShieldAlert, CheckCircle2, X, Link2, ExternalLink, Download, AlertCircle, History,
  Scale, Info, User, Landmark, ListChecks, Target, Layers,
  Star, Pencil, Trash2, Loader2
} from "lucide-react";
import {
  useSubmitWork,
  useRevisionAction,
  useUpdateDeliverable,
  useDeleteDeliverable,
} from "./contractMutations";
import { formatDateDMY } from "../../../utils/date";
import { useChatActions } from "../../../components/chat/chatMutations";
import { useModal } from "../../../hooks/modal/useModalStore";
import { useCreateReview } from "../../client/contract/contractMutations";
import SegmentedControl from "../../../components/common/SegmentedControl";
import { toastApiError, toastError, toastSuccess, toastWarning } from "../../../utils/toast";
import Button from "../../../components/common/Button";
import Dialog, { DialogClose } from "../../../components/common/Dialog";
import { Field } from "../../../components/common/Field";

const SUBTYPE_OPTIONS = [{ value: "file", label: "Direct Upload" }, { value: "link", label: "External Link" }];


export default function ContractDetail() {
  const { id } = useParams();
  const { data: contract, isLoading, isError } = useContractDetail(id);
  const createReviewMutation = useCreateReview();
  const submitWorkMutation = useSubmitWork();
  const revisionActionMutation = useRevisionAction();
  const { getRoomMutation } = useChatActions();
  const { openModal } = useModal();
  const [reviewForm, setReviewForm] = useState({ rating: 5, comment: "" });
  const [isReviewModalOpen, setIsReviewModalOpen] = useState(false);

  const [timeLeft, setTimeLeft] = useState({ days: 0, hours: 0, percentage: 0 });
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [isRejectModalOpen, setIsRejectModalOpen] = useState(false);
  const [subType, setSubType] = useState('file');
  const [formData, setFormData] = useState({ title: '', file: null, link_url: '', notes: '' });
  const [activeRevisionId, setActiveRevisionId] = useState(null);
  const [rejectionNote, setRejectionNote] = useState("");
  // Deliverable edit / delete. A submitted item can be corrected until the
  // contract leaves review; the API refuses anything after that with a 409 and
  // hides the controls, so the UI must agree with it.
  const [editingId, setEditingId] = useState(null);
  const [editForm, setEditForm] = useState({ title: "", notes: "", link_url: "", file: null });
  const [deletingId, setDeletingId] = useState(null);
  const updateMutation = useUpdateDeliverable();
  const deleteMutation = useDeleteDeliverable();
  const canManageDeliverables = ["active", "submitted"].includes(contract?.status);

  const startEdit = (item) => {
    setEditingId(item.id);
    setEditForm({
      title: item.title || "",
      notes: item.notes || "",
      link_url: item.link_url || "",
      file: null,
    });
  };

  const saveEdit = async () => {
    if (!editForm.title.trim()) return;
    const fd = new FormData();
    fd.append("title", editForm.title);
    fd.append("notes", editForm.notes || "");
    if (editForm.link_url) fd.append("link_url", editForm.link_url);
    if (editForm.file) fd.append("file", editForm.file);
    await updateMutation.mutateAsync({ id: editingId, formData: fd });
    setEditingId(null);
  };

  const confirmDelete = async () => {
    await deleteMutation.mutateAsync(deletingId);
    setDeletingId(null);
  };
  
  useEffect(() => {
    if (!contract) return;
    if (contract.status === 'completed') {
      setTimeLeft({ days: 0, hours: 0, percentage: 100 });
      return;
    }
    const signedAt = contract.freelancer_signed_at || contract.created_at;
    const days = Number(contract.delivery_days) || 0;
    if (signedAt && days > 0) {
      const calculateTime = () => {
        const start = new Date(signedAt).getTime();
        const durationMs = days * 24 * 60 * 60 * 1000;
        const end = start + durationMs;
        const now = new Date().getTime();
        const difference = end - now;
        if (difference > 0) {
          const d = Math.floor(difference / (1000 * 60 * 60 * 24));
          const h = Math.floor((difference % (1000 * 60 * 60 * 24)) / (1000 * 60 * 60));
          const totalDuration = end - start;
          const elapsed = now - start;
          const percentage = Math.min(Math.max((elapsed / totalDuration) * 100, 0), 100);
          setTimeLeft({ days: d, hours: h, percentage });
        } else {
          setTimeLeft({ days: 0, hours: 0, percentage: 100 });
        }
      };
      calculateTime();
      const timer = setInterval(calculateTime, 60000);
      return () => clearInterval(timer);
    }
  }, [contract?.freelancer_signed_at, contract?.delivery_days, contract?.status]);

  const handleOpenChat = async () => {
    try {
      // client_id is the client_profiles row id; the chat endpoint matches on
      // users.id, so prefer client_user_id when the API provides it.
      const targetId = contract.client_user_id || contract.client_id || contract.client;
      if (!targetId) {
        toastError("Client information missing.");
        return;
      }
      const room = await getRoomMutation.mutateAsync(targetId);
      openModal("messages", { initialRoomId: room.id });
    } catch (error) {
      toastError("Could not open chat. Please try again.");
    }
  };

  const handleSubmitReview = () => {
    if (!reviewForm.comment.trim()) return toastWarning("Please enter a comment");
    
    createReviewMutation.mutate({
      contract: id,
      reviewee: contract.client_id, 
      rating: reviewForm.rating,
      comment: reviewForm.comment,
      review_type: 'client' 
    }, {
      onSuccess: () => setIsReviewModalOpen(false)
    });
  };

  const handleRevisionDecision = (revisionId, action) => {
    if (action === 'reject') {
      setActiveRevisionId(revisionId);
      setIsRejectModalOpen(true);
      return;
    }
    revisionActionMutation.mutate({ revisionId, action }, {
      onSuccess: () => toastSuccess("Revision accepted. Status updated to Active."),
      onError: (err) => toastApiError(err, "Error updating revision")
    });
  };

  const confirmRejection = () => {
    if (!rejectionNote.trim()) return toastWarning("Please provide a reason for rejection");
    revisionActionMutation.mutate({
      revisionId: activeRevisionId,
      action: 'reject',
      message: rejectionNote
    }, {
      onSuccess: () => {
        setIsRejectModalOpen(false);
        setRejectionNote("");
        setActiveRevisionId(null);
        toastSuccess("Revision rejected");
      }
    });
  };

  const handleWorkSubmission = async () => {
    const data = new FormData();
    data.append('deliverable_type', subType);
    data.append('title', formData.title);
    data.append('notes', formData.notes || "");
    if (subType === 'file') {
      if (!formData.file) return toastWarning("Please select a file");
      data.append('file', formData.file);
    } else {
      if (!formData.link_url) return toastWarning("Please enter a URL");
      data.append('link_url', formData.link_url);
    }
    submitWorkMutation.mutate({ id, formData: data }, {
      onSuccess: () => {
        setIsModalOpen(false);
        setFormData({ title: '', file: null, link_url: '', notes: '' });
        toastSuccess("Work submitted successfully");
      }
    });
  };

  if (isLoading) return <div className="min-h-screen flex items-center justify-center font-medium text-fg-subtle">Loading Order Interface...</div>;
  if (isError || !contract) return <div className="min-h-screen flex items-center justify-center text-danger-soft-fg font-bold">Error loading order details.</div>;

  const getDueDateDisplay = () => {
    const signedAt = contract.freelancer_signed_at || contract.created_at;
    if (!signedAt) return '---';
    const date = new Date(signedAt);
    date.setDate(date.getDate() + Number(contract.delivery_days || 0));
    return formatDateDMY(date.toISOString());
  };

  const dispute = contract.dispute_details;
  const isSelfDisputed = dispute && dispute.raised_by_id === contract.freelancer_user_id;
  const isCurrentlyDisputed = ['disputed', 'dispute'].includes(contract.status);
  const clientReview = contract.reviews.client_review
  const myReview = contract.reviews.freelancer_review

  return (
    <div className="min-h-screen bg-surface-2 pb-20">
      <div className="max-w-page-2xl mx-auto px-6 lg:px-12 py-10 lg:py-14">

        <div className="mb-10 flex flex-col md:flex-row md:items-end justify-between gap-6 border-b border-line pb-8">
          <div className="min-w-0 flex-1">
            <div className="flex items-center gap-2 mb-2">
              <Briefcase className="w-5 h-5 text-brand shrink-0" />
              <span className="text-xs font-bold text-brand uppercase tracking-widest truncate">Workspace Dashboard</span>
            </div>
            <h1 className="text-3xl md:text-4xl font-extrabold text-fg tracking-tight break-words">Order Execution</h1>
            <p className="text-base md:text-lg text-fg-subtle mt-1 break-words">Order #ORD-{contract.id + 8000} • Vendor Workspace</p>
          </div>
          <div className="flex gap-3 shrink-0">
            <button onClick={handleOpenChat} className="group inline-flex items-center gap-2 px-6 py-3 bg-surface border border-line rounded-lg text-sm font-semibold text-fg-muted hover:bg-surface-2 transition-all shadow-sm whitespace-nowrap">
              Open Workspace Chat <MessageSquare className="w-4 h-4" />
            </button>
          </div>
        </div>

        {dispute && (
          <div className="mb-10 bg-surface border border-line rounded-xl shadow-sm overflow-hidden">
            <div className="bg-surface-2 px-4 md:px-8 py-5 border-b border-line flex flex-col sm:flex-row sm:items-center justify-between gap-4">
              <div className="flex items-center gap-4">
                <div className="p-2.5 bg-warning-soft rounded-lg text-warning shrink-0">
                  <Scale className="w-5 h-5" />
                </div>
                <div className="min-w-0">
                  <div className="flex flex-wrap items-center gap-2">
                    <h3 className="text-base font-bold text-fg uppercase">Arbitration Status</h3>
                    <span className={`text-[10px] px-2 py-0.5 rounded border font-bold uppercase ${isSelfDisputed ? 'bg-brand-soft text-brand border-brand-line' : 'bg-warning-soft text-warning-soft-fg border-warning/30'}`}>
                      {isSelfDisputed ? "Raised by You" : "Raised by Client"}
                    </span>
                  </div>
                  <p className="text-xs text-fg-subtle font-medium truncate">Case Reference: #{dispute.id}</p>
                </div>
              </div>
              <span className={`self-start sm:self-center px-4 py-1.5 rounded-md text-xs font-bold uppercase tracking-wider border whitespace-nowrap ${dispute.status === 'rejected' ? 'bg-danger-soft text-danger border-danger/30' : 'bg-warning-soft text-warning border-warning/30'
                }`}>
                {dispute.status}
              </span>
            </div>
            <div className="p-6 md:p-8 grid grid-cols-1 md:grid-cols-2 gap-8 md:gap-10">
              <div className="space-y-4">
                <div className="flex items-center gap-2 text-fg-subtle">
                  <Info className="w-4 h-4 shrink-0" />
                  <h4 className="text-[10px] md:text-xs font-bold uppercase tracking-widest">Dispute Foundation</h4>
                </div>
                <div className="p-5 bg-surface-2 border border-line-soft rounded-lg">
                  <p className="text-sm font-semibold text-fg leading-relaxed break-words whitespace-pre-wrap">{dispute.reason}</p>
                  <p className="text-[11px] text-fg-subtle mt-3 font-bold uppercase">Logged: {new Date(dispute.created_at).toLocaleDateString()}</p>
                </div>
              </div>
              <div className="space-y-4 md:border-l md:border-line-soft md:pl-10">
                <div className="flex items-center gap-2 text-fg-subtle">
                  <Landmark className="w-4 h-4 shrink-0" />
                  <h4 className="text-[10px] md:text-xs font-bold uppercase tracking-widest">Admin Determination</h4>
                </div>
                <p className="text-sm text-fg-muted leading-relaxed font-medium italic break-words whitespace-pre-wrap">
                  "{dispute.admin_notes || "Determination pending administrative review. Delivery and payment cycles are temporarily paused."}"
                </p>
                {dispute.resolved_at && (
                  <div className="flex items-center gap-2 mt-4 text-success">
                    <CheckCircle2 className="w-4 h-4 shrink-0" />
                    <span className="text-[10px] md:text-xs font-bold uppercase tracking-widest">Resolved on {new Date(dispute.resolved_at).toLocaleDateString()}</span>
                  </div>
                )}
              </div>
            </div>
          </div>
        )}

        {(contract.status === 'completed' || contract.status === 'refunded') && (
          <div className="mb-10 grid grid-cols-1 md:grid-cols-2 gap-8">
            {/* Left: Review from Client */}
            <div className="bg-surface rounded-xl border border-line shadow-sm p-8">
              <h2 className="text-[11px] font-bold text-fg-subtle uppercase tracking-widest mb-6 flex items-center gap-2">
                <Star className="w-4 h-4 text-warning fill-yellow-400" /> Client's Feedback
              </h2>
              {clientReview ? (
                <div className="space-y-4">
                  <div className="flex gap-1">
                    {[...Array(5)].map((_, i) => (
                      <Star key={i} size={16} className={i < clientReview.rating ? "text-warning fill-yellow-400" : "text-fg-subtle"} />
                    ))}
                  </div>
                  <p className="text-sm text-fg-muted italic leading-relaxed">"{clientReview.comment}"</p>
                  <p className="text-[10px] text-fg-subtle font-bold uppercase tracking-tight">Received on {formatDateDMY(clientReview.created_at)}</p>
                </div>
              ) : (
                <div className="py-4 text-center border border-dashed border-line-soft rounded-lg">
                  <p className="text-xs text-fg-subtle font-medium">Client hasn't left a review yet.</p>
                </div>
              )}
            </div>

            {/* Right: Your Review for Client */}
            <div className="bg-surface rounded-xl border border-line shadow-sm p-8">
              <h2 className="text-[11px] font-bold text-fg-subtle uppercase tracking-widest mb-6 flex items-center gap-2">
                <User className="w-4 h-4 text-brand" /> Your Review for Client
              </h2>
              {myReview ? (
                <div className="space-y-4">
                  <div className="flex gap-1">
                    {[...Array(5)].map((_, i) => (
                      <Star key={i} size={16} className={i < myReview.rating ? "text-brand fill-blue-500" : "text-fg-subtle"} />
                    ))}
                  </div>
                  <p className="text-sm text-fg-muted leading-relaxed italic">"{myReview.comment}"</p>
                </div>
              ) : (
                <button 
                  onClick={() => setIsReviewModalOpen(true)}
                  className="w-full py-4 border-2 border-dashed border-line rounded-xl text-fg-subtle hover:text-brand hover:border-brand-line transition-all text-xs font-bold uppercase tracking-widest flex items-center justify-center gap-2"
                >
                  <Target className="w-4 h-4" /> Rate your experience with client
                </button>
              )}
            </div>
          </div>
        )}
        <div className="bg-surface rounded-xl border border-line shadow-sm overflow-hidden mb-10">
          <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-5 divide-y md:divide-y-0 md:divide-x divide-slate-100">
            <div className="p-6 md:p-8">
              <p className="text-[11px] font-bold text-fg-subtle uppercase tracking-widest mb-2">Order Reference</p>
              <p className="text-lg md:text-xl font-bold text-fg break-all">#ORD-{contract.id + 8000}</p>
            </div>

            <div className="p-6 md:p-8 flex items-center gap-4 min-w-0">
              <div className="w-10 h-10 rounded-full bg-surface-3 flex items-center justify-center shrink-0 overflow-hidden border border-line">
                {contract.profile_photo ? (
                  <img src={contract.profile_photo} alt="Client" className="w-full h-full object-cover" />
                ) : (
                  <User className="w-5 h-5 text-fg-subtle" />
                )}
              </div>
              <div className="min-w-0">
                <p className="text-[11px] font-bold text-fg-subtle uppercase tracking-widest mb-0.5">Client</p>
                <p className="text-sm font-bold text-fg truncate">{contract.client_name}</p>
              </div>
            </div>

            <div className="p-6 md:p-8">
              <p className="text-[11px] font-bold text-fg-subtle uppercase tracking-widest mb-2">Project Value</p>
              <p className="text-lg md:text-xl font-bold text-fg break-words">${Number(contract.total_amount).toLocaleString()}</p>
            </div>

            <div className="p-6 md:p-8">
              <p className="text-[11px] font-bold text-fg-subtle uppercase tracking-widest mb-2">Payment Status</p>
              <div className="flex items-center gap-2">
                <span className={`w-2 h-2 rounded-full shrink-0 ${contract.payment_status === 'released' ? 'bg-emerald-500' :
                    contract.payment_status === 'refunded' ? 'bg-rose-500' : 'bg-warning-soft'
                  }`} />
                <p className="text-lg md:text-xl font-bold text-fg uppercase truncate">
                  {contract.payment_status ? contract.payment_status.replace(/_/g, ' ') : 'Pending'}
                </p>
              </div>
            </div>

            <div className="p-6 md:p-8 bg-surface-2/50">
              <p className="text-[11px] font-bold text-fg-subtle uppercase tracking-widest mb-2">Contract Status</p>
              <div className="flex items-center gap-2">
                <span className={`w-2 h-2 rounded-full shrink-0 ${contract.status === 'active' ? 'bg-brand-soft animate-pulse' : 'bg-success-soft'}`} />
                <p className="text-lg md:text-xl font-bold text-fg uppercase truncate">{contract.status}</p>
              </div>
            </div>
          </div>
        </div>

        <div className="grid grid-cols-1 lg:grid-cols-12 gap-10">
          <div className="lg:col-span-8 space-y-10 min-w-0">
            <div className="bg-surface rounded-xl border border-line shadow-sm p-6 md:p-10">
              <div className="flex flex-col sm:flex-row sm:items-center justify-between mb-8 border-b border-line-soft pb-6 gap-4">
                <h2 className="text-xl font-bold text-fg flex items-center gap-3 uppercase tracking-tight">
                  <FileText className="w-6 h-6 text-fg-subtle shrink-0" />
                  Project Brief
                </h2>
                <div className="flex items-center gap-2 px-3 py-1 bg-surface-3 rounded-full text-fg-muted self-start sm:self-center">
                  <Tag className="w-3.5 h-3.5" />
                  <span className="text-[10px] font-bold uppercase truncate max-w-[150px]">{contract.project_category}</span>
                </div>
              </div>

              <div className="space-y-8">
                <div className="min-w-0">
                  <h3 className="text-lg md:text-xl font-bold text-fg break-words">{contract.project_title}</h3>
                </div>

                <div className="space-y-4">
                  <div className="flex items-center gap-2 text-fg-subtle">
                    <Info className="w-4 h-4 shrink-0" />
                    <h4 className="text-xs font-bold uppercase tracking-widest">Description</h4>
                  </div>
                  <div className="p-4 md:p-8 bg-surface-2 border border-line-soft rounded-lg text-fg-muted leading-relaxed whitespace-pre-wrap text-sm md:text-base break-words">
                    {contract.project_description}
                  </div>
                </div>

                <div className="grid grid-cols-1 md:grid-cols-2 gap-8">
                  <div className="space-y-4 min-w-0">
                    <div className="flex items-center gap-2 text-fg-subtle">
                      <ListChecks className="w-4 h-4 shrink-0" />
                      <h4 className="text-xs font-bold uppercase tracking-widest">Requirements</h4>
                    </div>
                    <div className="p-6 bg-surface-2/50 border border-line-soft rounded-lg text-sm text-fg-muted leading-relaxed whitespace-pre-wrap break-words">
                      {contract.requirements || "No specific requirements provided."}
                    </div>
                  </div>
                  <div className="space-y-4 min-w-0">
                    <div className="flex items-center gap-2 text-fg-subtle">
                      <Target className="w-4 h-4 shrink-0" />
                      <h4 className="text-xs font-bold uppercase tracking-widest">Expected Deliverables</h4>
                    </div>
                    <div className="p-6 bg-surface-2/50 border border-line-soft rounded-lg text-sm text-fg-muted leading-relaxed whitespace-pre-wrap break-words">
                      {contract.expected_deliverables || "No specific deliverables stated."}
                    </div>
                  </div>
                </div>

                <div className="space-y-4">
                  <div className="flex items-center gap-2 text-fg-subtle">
                    <Layers className="w-4 h-4 shrink-0" />
                    <h4 className="text-xs font-bold uppercase tracking-widest">Required Technologies</h4>
                  </div>
                  <div className="flex flex-wrap gap-2">
                    {contract.skills?.map((skill, i) => (
                      <span key={i} className="px-4 py-2 bg-surface border border-line rounded-lg text-[10px] md:text-xs font-bold text-fg-muted shadow-sm break-words max-w-full">
                        {skill}
                      </span>
                    ))}
                  </div>
                </div>
              </div>

              <div className="grid grid-cols-1 sm:grid-cols-3 gap-6 md:gap-8 mt-12 pt-8 border-t border-line-soft">
                <div className="min-w-0">
                  <p className="text-[11px] text-fg-subtle font-bold uppercase tracking-widest">Commencement</p>
                  <p className="text-sm md:text-base font-bold text-fg-muted mt-1 truncate">{formatDateDMY(contract.freelancer_signed_at || contract.created_at)}</p>
                </div>
                <div className="min-w-0">
                  <p className="text-[11px] text-fg-subtle font-bold uppercase tracking-widest">Hard Deadline</p>
                  <p className="text-sm md:text-base font-bold text-fg-muted mt-1 truncate">{getDueDateDisplay()}</p>
                </div>
                <div className="min-w-0">
                  <p className="text-[11px] text-fg-subtle font-bold uppercase tracking-widest">Delivery Window</p>
                  <p className="text-sm md:text-base font-bold text-brand mt-1 truncate">{contract.delivery_days} Working Days</p>
                </div>
              </div>
            </div>

            <div className="bg-surface rounded-xl border border-line shadow-sm overflow-hidden">
              <div className="px-6 md:px-10 py-6 border-b border-line-soft flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4 bg-surface-2/30">
                <h2 className="text-lg font-bold text-fg uppercase tracking-tight">Submitted Assets</h2>
                {/* A freelancer must be able to (re)submit while a dispute is open,
                otherwise the "Queue is empty" screen left no way to send the
                requested work. Submitted also stays valid so resubmitting is
                allowed after a revision request. */}
            {['active', 'disputed', 'submitted'].includes(contract.status) && (
                  <button onClick={() => setIsModalOpen(true)} className="w-full sm:w-auto px-5 py-2 bg-brand text-brand-fg text-[11px] font-bold rounded-md hover:bg-brand-hover transition-all uppercase tracking-widest shadow-lg shadow-blue-100">
                    Submit Deliverable
                  </button>
                )}
              </div>
              <div className="p-6 md:p-10">
                {contract.deliverables?.length > 0 ? (
                  <div className="space-y-4">
                    {contract.deliverables.map((item, index) => (
                      <div key={item.id} className="flex flex-col sm:flex-row items-start sm:items-center justify-between p-4 md:p-6 bg-surface rounded-lg border border-line-soft hover:border-line transition-all shadow-sm gap-4">
                        <div className="flex items-center gap-5 min-w-0 w-full">
                          <div className="p-3 md:p-4 bg-surface-2 text-fg-subtle rounded-lg border border-line-soft shrink-0">
                            {item.deliverable_type === 'link' ? <Link2 className="w-5 h-5" /> : <FileText className="w-5 h-5" />}
                          </div>
                          <div className="min-w-0 flex-1">
                            <p className="text-sm md:text-base font-bold text-fg break-words">{item.title}</p>
                            {/* `submitted_at` is the wire name (DeliverableResponse.
                                SubmittedAt); `created_at` is undefined and rendered a
                                dangling separator. */}
                            <p className="text-[10px] text-fg-subtle font-bold uppercase tracking-widest mt-1 truncate">{[item.deliverable_type, formatDateDMY(item.submitted_at, "—")].filter(Boolean).join(" • ")}</p>
                          </div>
                        </div>
                        <div className="w-full sm:w-auto flex items-center justify-end gap-2">
                          {item.file_url && (
                            <a href={item.file_url} target="_blank" rel="noreferrer" className="flex items-center justify-center p-3 hover:bg-surface-2 rounded-lg transition-colors text-fg-subtle hover:text-brand border border-line-soft sm:border-transparent hover:border-line-soft">
                              <Download className="w-5 h-5" />
                              <span className="sm:hidden ml-2 text-xs font-bold uppercase">Download</span>
                            </a>
                          )}
                          {canManageDeliverables && (
                            <button
                              onClick={() => startEdit(item)}
                              title="Edit deliverable"
                              className="flex items-center justify-center p-3 hover:bg-surface-2 rounded-lg transition-colors text-fg-subtle hover:text-warning border border-line-soft sm:border-transparent hover:border-line-soft"
                            >
                              <Pencil className="w-4 h-4" />
                            </button>
                          )}
                          {canManageDeliverables && (
                            <button
                              onClick={() => setDeletingId(item.id)}
                              title="Delete deliverable"
                              className="flex items-center justify-center p-3 hover:bg-danger-soft rounded-lg transition-colors text-fg-subtle hover:text-danger border border-line-soft sm:border-transparent hover:border-danger/30"
                            >
                              <Trash2 className="w-4 h-4" />
                            </button>
                          )}
                        </div>
                      </div>
                    ))}
                  </div>
                ) : (
                  <div className="text-center py-20 bg-surface-2/30 rounded-lg border border-dashed border-line p-6">
                    <History className="w-12 h-12 text-fg-subtle mx-auto mb-4" />
                    <p className="text-base font-bold text-fg">Queue is empty</p>
                    <p className="text-sm text-fg-subtle mt-1 max-w-xs mx-auto">Upload your first deliverable to begin the client review process.</p>
                  </div>
                )}
              </div>
            </div>
          </div>

          <div className="lg:col-span-4 space-y-8 min-w-0">
            <div className="bg-surface rounded-xl border border-line shadow-sm p-6 md:p-10">
              <h2 className="text-[11px] font-bold text-fg-subtle uppercase tracking-widest mb-8">Production Timeline</h2>
              <div className="mb-8">
                {!isCurrentlyDisputed && contract.status !== 'completed' ? (
                  <div className="space-y-1">
                    <div className="flex flex-wrap items-baseline gap-2">
                      <span className="text-3xl md:text-4xl font-black text-fg tracking-tighter">{timeLeft.days}D</span>
                      <span className="text-3xl md:text-4xl font-black text-fg tracking-tighter">{timeLeft.hours}H</span>
                    </div>
                    <p className="text-xs font-bold text-fg-subtle uppercase tracking-widest">Until Maturity</p>
                  </div>
                ) : (
                  <div className="flex items-center gap-3 p-4 bg-surface-2 rounded-lg border border-line-soft">
                    <AlertCircle className="w-5 h-5 text-fg-subtle shrink-0" />
                    <span className="text-xs md:text-sm font-bold text-fg-muted uppercase">Clock Suspended</span>
                  </div>
                )}
              </div>
              {/* The elapsed-time meter. `role="progressbar"` with the valuemin/max/now
                  trio because it is a determinate meter whose value changes over time -- a
                  screen reader otherwise announces nothing here, and the only other cue is
                  the "12D" countdown, which says how much is LEFT rather than how much has
                  elapsed. The accessible name names the contract, so a list of contracts does
                  not produce several identically-named progress bars. */}
              <div
                role="progressbar"
                aria-label={`Time elapsed on ${contract.project_title ?? "this contract"}`}
                aria-valuemin={0}
                aria-valuemax={100}
                aria-valuenow={Math.round(timeLeft.percentage)}
                aria-valuetext={`${Math.round(timeLeft.percentage)}% elapsed, ${timeLeft.days} days remaining`}
                className="relative w-full h-2 bg-surface-3 rounded-full overflow-hidden mb-8"
              >
                <div
                  className={`absolute left-0 top-0 h-full transition-all duration-progress rounded-full ${contract.status === 'completed' || contract.status === 'CANCELLED' ? 'bg-success-soft' :
                      isCurrentlyDisputed ? 'bg-warning' : 'bg-brand'
                    }`}
                  style={{ width: `${timeLeft.percentage}%` }}
                />
              </div>
              <div className="p-4 bg-brand-soft/50 rounded-lg border border-brand-line flex items-start gap-3">
                <ShieldAlert className="w-5 h-5 text-brand shrink-0 mt-0.5" />
                <p className="text-[11px] md:text-xs text-brand font-semibold leading-relaxed break-words">Funds are secured in Escrow and will be released upon client approval of final deliverables.</p>
              </div>
            </div>

            {contract.revisions?.length > 0 && (
              <div className="bg-surface rounded-xl border border-danger/30 shadow-sm overflow-hidden">
                <div className="bg-danger-soft/50 px-6 md:px-8 py-4 border-b border-red-50 flex items-center gap-2">
                  <History className="w-4 h-4 text-danger shrink-0" />
                  <h2 className="text-[10px] md:text-xs font-bold text-fg uppercase tracking-widest truncate">Modification Requests</h2>
                </div>
                <div className="p-6 md:p-8 space-y-6">
                  {contract.revisions.map((rev, index) => (
                    <div key={rev.id} className="space-y-4 pb-6 border-b border-line-soft last:border-0 last:pb-0">
                      <div className="flex justify-between items-center gap-2">
                        <span className="text-[10px] font-bold text-fg-subtle uppercase whitespace-nowrap">Request #{index + 1}</span>
                        <span className={`text-[9px] font-black uppercase px-2 py-0.5 rounded border whitespace-nowrap ${rev.status === 'pending' ? 'bg-warning-soft text-warning border-warning/30' :
                            rev.status === 'rejected' ? 'bg-danger-soft text-danger border-danger/30' : 'bg-success-soft text-success border-success/30'
                          }`}>
                          {rev.status}
                        </span>
                      </div>
                      <p className="text-sm text-fg-muted leading-relaxed font-medium break-words">"{rev.reason}"</p>
                      {rev.status === 'pending' && !isCurrentlyDisputed && contract.status !== 'completed' && (
                        <div className="flex flex-col sm:flex-row gap-2">
                          <button onClick={() => handleRevisionDecision(rev.id, 'accept')} className="flex-1 py-2 bg-brand text-brand-fg text-[10px] font-bold rounded-md hover:bg-brand-hover uppercase tracking-widest transition-all">Accept</button>
                          <button onClick={() => handleRevisionDecision(rev.id, 'reject')} className="flex-1 py-2 bg-surface border border-line text-fg-muted text-[10px] font-bold rounded-md hover:bg-surface-2 uppercase tracking-widest transition-all">Decline</button>
                        </div>
                      )}
                    </div>
                  ))}
                </div>
              </div>
            )}

            {contract.legal_document_url && (
              <div className="bg-surface rounded-xl border border-line shadow-sm p-6 md:p-8 hover:border-brand-line transition-colors group">
                <div className="flex items-center gap-5 mb-8">
                  <div className="p-3 bg-surface-2 text-fg-subtle rounded-lg group-hover:text-brand group-hover:bg-brand-hover-soft transition-colors shrink-0">
                    <Landmark className="w-6 h-6" />
                  </div>
                  <div className="min-w-0">
                    <h3 className="text-[10px] md:text-xs font-bold text-fg uppercase tracking-widest truncate">Service Agreement</h3>
                    <p className="text-[10px] text-fg-subtle font-bold truncate">LEGALLY BINDING DOCUMENT</p>
                  </div>
                </div>
                <a href={contract.legal_document_url} target="_blank" rel="noopener noreferrer" className="w-full flex items-center justify-center gap-3 py-4 bg-surface-2 text-fg-muted text-[10px] md:text-xs font-bold rounded-lg hover:bg-surface-3 border border-line-soft uppercase tracking-widest transition-all">
                  <Download className="w-4 h-4 shrink-0" /> <span className="truncate">Download Agreement</span>
                </a>
              </div>
            )}

            {!dispute?.id && (
              <div className="grid grid-cols-1 gap-4">
                {isCurrentlyDisputed ? (
                  <div className="w-full flex items-center justify-center gap-3 py-5 bg-surface-2 border border-line text-fg-subtle text-[10px] md:text-[11px] font-bold rounded-lg uppercase tracking-widest shadow-inner px-4 text-center">
                    <ShieldAlert className="w-4 h-4 shrink-0" /> Arbitration Review In Progress
                  </div>
                ) : (
                  <button
                    onClick={() => openModal("raise-dispute", { contractId: id })}
                    className="w-full flex items-center justify-center gap-3 py-5 bg-surface border border-red-50 text-danger text-[10px] md:text-[11px] font-bold rounded-lg hover:bg-danger-soft transition-all uppercase tracking-widest px-4 text-center"
                  >
                    <ShieldAlert className="w-4 h-4 shrink-0" /> Raise Dispute
                  </button>
                )}
              </div>
            )}
          </div>
        </div>
      </div>

      {/* Hand-rolled scrim: no Escape (WCAG 2.1.2), no focus trap, no focus
          restoration, no role="dialog", and the page behind stayed keyboard-
          reachable. <Dialog> supplies all of it.

          The textarea had no label element at all -- not a broken one, none --
          so a static scan for unassociated <label>s cannot see it. It is named
          here for the first time. */}
      <Dialog
        open={isRejectModalOpen}
        onClose={() => setIsRejectModalOpen(false)}
        title="Decline Modification"
        size="md"
        className="p-0 overflow-hidden"
      >
        <div className="px-6 md:px-10 py-6 md:py-8 border-b border-line-soft flex justify-between items-start">
          <h3 className="text-xl font-bold text-fg tracking-tight">Decline Modification</h3>
          <DialogClose className="shrink-0"><X className="w-6 h-6" aria-hidden="true" /></DialogClose>
        </div>
        <div className="p-6 md:p-10 space-y-6">
          <div className="p-4 bg-danger-soft border border-danger/30 rounded-lg flex gap-3">
            <AlertCircle className="w-5 h-5 text-danger shrink-0 mt-0.5" aria-hidden="true" />
            <p className="text-xs text-danger-soft-fg font-semibold leading-relaxed break-words">Declining a revision may lead to a dispute. Clearly state why the request falls outside the original project scope.</p>
          </div>
          <Field
            label="Reason for declining"
            hint="Required, and shown to the client if this escalates to a dispute."
          >
            {(f) => (
              <textarea
                rows="4"
                {...f}
                className="w-full p-5 bg-surface-2 border border-line rounded-lg text-sm focus-visible:ring-2 focus-visible:ring-danger/20 outline-none resize-none font-medium break-words"
                placeholder="State your technical or contractual reasoning..."
                value={rejectionNote}
                onChange={(e) => setRejectionNote(e.target.value)}
              />
            )}
          </Field>
          <div className="flex flex-col sm:flex-row gap-4">
            <button onClick={() => setIsRejectModalOpen(false)} className="flex-1 py-4 bg-surface border border-line text-fg-muted text-[10px] md:text-xs font-bold rounded-lg uppercase tracking-widest hover:bg-surface-2">Cancel</button>
            <button onClick={confirmRejection} disabled={revisionActionMutation.isPending} className="flex-1 py-4 bg-danger text-danger-fg text-[10px] md:text-xs font-bold rounded-lg uppercase tracking-widest hover:bg-danger shadow-lg shadow-red-100">
              {revisionActionMutation.isPending ? "Processing..." : "Confirm Decline"}
            </button>
          </div>
        </div>
      </Dialog>
      <Dialog
        open={isReviewModalOpen}
        onClose={() => setIsReviewModalOpen(false)}
        title="Rate the Client"
        description={`Order #ORD-${contract.id + 8000}`}
        size="lg"
        className="p-0 overflow-hidden"
      >
        <div className="p-8 border-b border-line-soft flex justify-between items-center">
          <div>
            <h3 className="text-xl font-black text-fg uppercase tracking-tight">Rate the Client</h3>
            <p className="text-xs text-fg-subtle font-bold uppercase mt-1">Order #ORD-{contract.id + 8000}</p>
          </div>
          <DialogClose className="rounded-full">
            <X className="w-5 h-5 text-fg-subtle" aria-hidden="true" />
          </DialogClose>
        </div>
        <div className="p-8 space-y-8">
          <div className="text-center">
            {/* A group of five mutually exclusive choices, and each star button
                was previously unlabelled -- a screen reader announced five
                identical buttons. `aria-label` names them by value and
                `aria-pressed` exposes the current rating, which was conveyed by
                fill colour alone. */}
            <fieldset>
              <legend className="text-xs font-bold text-fg-subtle uppercase tracking-widest mb-4">Overall Experience</legend>
              <div className="flex justify-center gap-3">
                {[1, 2, 3, 4, 5].map((num) => (
                  <button
                    key={num}
                    type="button"
                    role="radio"
                    aria-checked={reviewForm.rating === num}
                    aria-label={`${num} ${num === 1 ? "star" : "stars"}`}
                    onClick={() => setReviewForm({ ...reviewForm, rating: num })}
                    className="transition-transform active:scale-90"
                  >
                    <Star
                      size={32}
                      aria-hidden="true"
                      className={num <= reviewForm.rating ? "text-warning fill-yellow-400" : "text-fg-subtle"}
                    />
                  </button>
                ))}
              </div>
            </fieldset>
          </div>
          <div className="space-y-3">
            <Field
              label="Public Feedback"
              hint="Visible to the client once you submit."
            >
              {(f) => (
                <textarea
                  rows={4}
                  value={reviewForm.comment}
                  onChange={(e) => setReviewForm({ ...reviewForm, comment: e.target.value })}
                  placeholder="How was the communication and clarity of requirements?"
                  {...f}
                  className="w-full p-4 bg-surface-2 border border-line rounded-xl text-sm font-medium focus-visible:ring-2 focus-visible:ring-brand/20 focus-visible:border-brand outline-none transition-all resize-none"
                />
              )}
            </Field>
          </div>
          <button
            onClick={handleSubmitReview}
            disabled={createReviewMutation.isPending}
            className="w-full py-5 bg-brand text-brand-fg rounded-xl font-black text-xs uppercase tracking-[0.2em] hover:bg-brand-hover shadow-xl shadow-blue-100 transition-all disabled:opacity-50"
          >
            {createReviewMutation.isPending ? "Submitting..." : "Submit Review"}
          </button>
        </div>
      </Dialog>


      <Dialog
        open={isModalOpen}
        onClose={() => setIsModalOpen(false)}
        title="Submit Deliverable"
        description={`Order #ORD-${contract.id + 8000}`}
        size="lg"
        className="p-0 overflow-hidden"
      >
        <div className="px-10 py-8 border-b border-line-soft flex justify-between items-start">
          <div>
            <h3 className="text-xl font-bold text-fg tracking-tight">Submit Deliverable</h3>
            <p className="text-xs text-fg-subtle font-bold uppercase tracking-widest mt-1">Order #ORD-{contract.id + 8000}</p>
          </div>
          <DialogClose><X className="w-6 h-6" aria-hidden="true" /></DialogClose>
        </div>
        <div className="p-10 space-y-6">
              <SegmentedControl
                layoutId="deliverable-subtype-pill"
                size="lg"
                value={subType}
                onChange={setSubType}
                options={SUBTYPE_OPTIONS}
              />
              <Field label="Asset Label">
                {(f) => (
                  <input
                    type="text"
                    placeholder="e.g. Final Identity Design - V1"
                    value={formData.title}
                    onChange={(e) => setFormData({ ...formData, title: e.target.value })}
                    {...f}
                    className="w-full p-4 bg-surface-2 border border-line rounded-lg text-sm focus-visible:ring-2 focus-visible:ring-brand/20 outline-none font-medium"
                  />
                )}
              </Field>
              {subType === 'file' ? (
                /* The one control here that was already right: an explicit
                   id/htmlFor pair on a visually hidden file input. Left alone
                   deliberately -- it is the pattern the other three should have
                   used, and `hidden` on the input is legitimate because the
                   visible <label> is its own control surface. */
                <div className="border-2 border-dashed border-line rounded-xl p-10 text-center hover:border-brand-line transition-colors bg-surface-2/50">
                  <input type="file" id="dropzone-file" className="hidden" onChange={(e) => setFormData({ ...formData, file: e.target.files[0] })} />
                  <label htmlFor="dropzone-file" className="cursor-pointer">
                    <Upload className="w-8 h-8 text-brand mx-auto mb-3" aria-hidden="true" />
                    <p className="text-sm font-bold text-fg-muted">{formData.file ? formData.file.name : "Select Asset File"}</p>
                    <p className="text-[10px] text-fg-subtle mt-2 uppercase font-bold">Standard Formats Up to 50MB</p>
                  </label>
                </div>
              ) : (
                <Field label="Resource URL">
                  {(f) => (
                    <input
                      type="text"
                      placeholder="https://..."
                      value={formData.link_url}
                      onChange={(e) => setFormData({ ...formData, link_url: e.target.value })}
                      {...f}
                      className="w-full p-4 bg-surface-2 border border-line rounded-lg text-sm focus-visible:ring-2 focus-visible:ring-brand/20 outline-none font-medium"
                    />
                  )}
                </Field>
              )}
              <button onClick={handleWorkSubmission} disabled={submitWorkMutation.isPending} className="w-full py-5 bg-brand text-brand-fg text-xs font-bold rounded-lg hover:bg-brand-hover uppercase tracking-widest shadow-lg shadow-blue-100 transition-all">
                {submitWorkMutation.isPending ? "Uploading Asset..." : "Confirm Submission"}
              </button>
        </div>
      </Dialog>
      {/* Both were hand-rolled `fixed inset-0 z-50` scrims: no focus trap, no Esc,
          no scroll lock, no `aria-modal` -- so the page behind stayed
          keyboard-reachable while a dialog was open. The four labels also had no
          `htmlFor` and no matching id, so they announced as bare "edit text". */}
      <Dialog
        open={editingId !== null}
        onClose={() => setEditingId(null)}
        title="Edit Deliverable"
        size="lg"
      >
        <div className="space-y-4">
          <Field label="Title">
            {(f) => (
              <input
                type="text"
                value={editForm.title}
                onChange={(e) => setEditForm({ ...editForm, title: e.target.value })}
                {...f}
                className="w-full p-4 bg-surface-2 border border-line rounded-lg text-sm focus-visible:ring-2 focus-visible:ring-warning/20 outline-none"
              />
            )}
          </Field>
          <Field label="Notes">
            {(f) => (
              <textarea
                value={editForm.notes}
                onChange={(e) => setEditForm({ ...editForm, notes: e.target.value })}
                {...f}
                className="w-full p-4 bg-surface-2 border border-line rounded-lg text-sm focus-visible:ring-2 focus-visible:ring-warning/20 outline-none min-h-[90px]"
              />
            )}
          </Field>
          <Field label="Link (optional)" hint="A public URL for the deliverable, if there is one.">
            {(f) => (
              <input
                type="text"
                placeholder="https://..."
                value={editForm.link_url}
                onChange={(e) => setEditForm({ ...editForm, link_url: e.target.value })}
                {...f}
                className="w-full p-4 bg-surface-2 border border-line rounded-lg text-sm focus-visible:ring-2 focus-visible:ring-warning/20 outline-none"
              />
            )}
          </Field>
          {/* Left as a raw input: `Field` is for text controls, and a file input
              needs its own label association rather than a text-field wrapper. */}
          <Field label="Replace file (optional)">
            {(f) => (
              <input
                type="file"
                onChange={(e) => setEditForm({ ...editForm, file: e.target.files[0] })}
                {...f}
                className="w-full text-xs text-fg-muted file:mr-3 file:py-2 file:px-4 file:rounded-lg file:border-0 file:bg-surface-3 file:text-xs file:font-bold"
              />
            )}
          </Field>
        </div>
        <div className="flex gap-3 mt-6">
          <button
            type="button"
            onClick={() => setEditingId(null)}
            className="flex-1 py-3 rounded-lg border border-line text-xs font-bold text-fg-muted hover:bg-surface-2 transition-all"
          >
            Cancel
          </button>
          <Button
            onClick={saveEdit}
            variant="secondary"
            size="none"
            loading={updateMutation.isPending}
            disabled={updateMutation.isPending || !editForm.title.trim()}
            className="flex-1 py-3 text-xs"
          >
            Save Changes
          </Button>
        </div>
      </Dialog>

      <Dialog
        open={deletingId !== null}
        onClose={() => setDeletingId(null)}
        title="Delete Deliverable?"
        description="This removes the submission and its file reference. This action is permanent and cannot be undone."
        size="sm"
        footer={
          <div className="flex gap-3">
            <button
              type="button"
              onClick={() => setDeletingId(null)}
              className="flex-1 py-3 rounded-lg border border-line text-xs font-bold text-fg-muted hover:bg-surface-2 transition-all"
            >
              Cancel
            </button>
            <Button
              onClick={confirmDelete}
              variant="danger"
              size="none"
              loading={deleteMutation.isPending}
              className="flex-1 py-3 text-xs"
            >
              Delete
            </Button>
          </div>
        }
      >
        <div className="w-14 h-14 mx-auto bg-danger-soft rounded-full flex items-center justify-center">
          <AlertCircle className="w-7 h-7 text-danger" aria-hidden="true" />
        </div>
      </Dialog>
    </div>

  );
}


