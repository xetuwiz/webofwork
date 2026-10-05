import React, { useState } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { useTicketDetails } from "./supportQueries";
import { useReplyMutation } from "./supportMutations";
import { 
  Loader2, 
  ChevronLeft, 
  Send, 
  CheckCircle, 
  User, 
  Mail, 
  Calendar, 
  BadgeCheck,
  MessageSquare, 
} from "lucide-react";

export default function SupportTicketDetail() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [replyText, setReplyText] = useState("");

  const { data: ticket, isLoading } = useTicketDetails(id);
  const mutation = useReplyMutation();

  const isResolved = ticket?.status === 'resolved';
  const formatTicketId = (id) => `TKT-${String(id).padStart(3, "0")}`;

  const handleSendReply = () => {
    if (!replyText.trim()) return;
    mutation.mutate({ id, admin_reply: replyText }, {
      onSuccess: () => setReplyText("")
    });
  };

  if (isLoading) return (
    <div className="min-h-screen flex items-center justify-center bg-surface">
      <Loader2 className="animate-spin text-fg-subtle w-6 h-6" />
    </div>
  );

  return (
    <div className="min-h-screen bg-surface text-fg font-sans antialiased">
      {/* Top Navigation Bar */}
      <div className="bg-surface border-b border-line sticky top-0 z-10">
        <div className="max-w-7xl mx-auto px-6 h-16 flex items-center justify-between">
          <button 
            onClick={() => navigate(-1)} 
            className="flex items-center gap-2 text-sm font-medium text-fg-subtle hover:text-fg transition-colors"
          >
            <ChevronLeft className="w-4 h-4" /> Back to list
          </button>
          <div className="flex items-center gap-3">
            <span className="text-xs font-medium text-fg-subtle">Status:</span>
            <span className={`text-[11px] font-bold uppercase tracking-wider px-2.5 py-1 rounded-md border ${
              isResolved 
                ? 'bg-success-soft border-success/30 text-success' 
                : 'bg-brand-soft border-brand-line text-brand'
            }`}>
              {ticket.status}
            </span>
          </div>
        </div>
      </div>

      <div className="max-w-7xl mx-auto px-6 py-10">
        <div className="grid grid-cols-12 gap-10">
          
          {/* Main Content Area */}
          <div className="col-span-12 lg:col-span-8 space-y-8">
            
            {/* Header Section */}
            <div className="space-y-2">
              <p className="text-xs font-semibold text-brand uppercase tracking-[0.15em]">
                {ticket.sender_role} Request • {formatTicketId(ticket.id)}
              </p>
              <h1 className="text-3xl font-semibold text-fg tracking-tight">
                {ticket.subject}
              </h1>
            </div>

            {/* Original Message Card */}
            <div className="bg-surface border border-line rounded-xl shadow-[0_1px_3px_rgba(0,0,0,0.05)] overflow-hidden">
              <div className="px-6 py-4 border-b border-line-soft bg-surface-2/50 flex items-center justify-between">
                <div className="flex items-center gap-2">
                  <MessageSquare className="w-4 h-4 text-fg-subtle" />
                  <span className="text-sm font-semibold text-fg-muted">Inquiry Details</span>
                </div>
                <span className="text-xs text-fg-subtle">
                  {new Date(ticket.created_at).toLocaleString()}
                </span>
              </div>
              <div className="p-8">
                <p className="text-[15px] text-fg-muted leading-relaxed whitespace-pre-wrap">
                  {ticket.message}
                </p>
              </div>
            </div>

            {/* Response Section */}
            {isResolved ? (
              <div className="bg-surface border border-line rounded-xl p-10 text-center flex flex-col items-center">
                <div className="w-12 h-12 bg-success-soft text-success rounded-full flex items-center justify-center mb-4">
                  <CheckCircle className="w-6 h-6" />
                </div>
                <h3 className="text-lg font-semibold text-fg">This conversation is closed</h3>
                <p className="text-sm text-fg-subtle mt-1 max-w-sm mx-auto">
                  A resolution was sent on {new Date(ticket.resolved_at).toLocaleDateString()}. 
                  The ticket is now archived for internal record keeping.
                </p>
              </div>
            ) : (
              <div className="space-y-4">
                <div className="flex items-center justify-between">
                  <h3 className="text-sm font-bold text-fg uppercase tracking-wider">Your Response</h3>
                  <span className="text-xs text-fg-subtle italic">This will be sent via official email</span>
                </div>
                <div className="bg-surface border border-line rounded-xl shadow-sm focus-within:ring-1 focus-within:ring-primary focus-within:border-primary transition-all">
                  <textarea
                    value={replyText}
                    onChange={(e) => setReplyText(e.target.value)}
                    placeholder="Provide a detailed solution..."
                    className="w-full h-56 p-6 bg-transparent outline-none text-[15px] text-fg-muted resize-none"
                  />
                  <div className="px-6 py-4 border-t border-line-soft flex items-center justify-between bg-surface-2/30">
                    <p className="text-xs text-fg-subtle font-medium">
                      Submitting will mark this ticket as <span className="text-fg-muted font-bold">Resolved</span>
                    </p>
                    <button
                      onClick={handleSendReply}
                      disabled={mutation.isPending || !replyText.trim()}
                      className="flex items-center gap-2 px-6 py-2.5 bg-inverse text-white rounded-lg font-semibold text-sm hover:bg-inverse transition-all disabled:opacity-20 shadow-sm shadow-gray-200"
                    >
                      {mutation.isPending ? "Sending..." : "Submit & Close"}
                      <Send className="w-3.5 h-3.5" />
                    </button>
                  </div>
                </div>
              </div>
            )}
          </div>

          {/* Sidebar Area */}
          <div className="col-span-12 lg:col-span-4 space-y-6">
            <div className="bg-surface border border-line rounded-xl p-6 shadow-sm">
              <h3 className="text-xs font-bold text-fg-subtle uppercase tracking-widest mb-6 border-b border-line-soft pb-3">
                Information Panel
              </h3>
              
              <div className="space-y-6">
                {/* "Customer Name", "Email Address" and "Created Date" were all
                    <label>s. Each one heads a <span> of read-only display text,
                    not a form control -- so there was nothing for htmlFor to
                    point at, and the element was simply the wrong one. They are
                    now <p>s. This is the third distinct misuse found in the
                    sweep, alongside labels closed before their control and
                    labels heading a group of several. */}
                <div className="flex gap-4">
                  <User className="w-4 h-4 text-fg-subtle mt-1" aria-hidden="true" />
                  <div>
                    <p className="block text-[10px] font-bold text-fg-subtle uppercase tracking-wider">Customer Name</p>
                    {/* Falls back rather than rendering blank.
                    This field used to carry the user's GUID, so it was never empty
                    and no fallback was needed. It now carries the real name, which
                    is null when the ticket's user row is gone -- and a blank
                    "Customer Name" is worse than saying so, because the admin
                    cannot tell a missing name from a rendering fault. */}
                  <span className="text-sm font-semibold text-fg">
                    {ticket.user_full_name || "Name unavailable"}
                  </span>
                  </div>
                </div>

                <div className="flex gap-4">
                  <Mail className="w-4 h-4 text-fg-subtle mt-1" />
                  <div className="min-w-0">
                    <p className="block text-[10px] font-bold text-fg-subtle uppercase tracking-wider">Email Address</p>
                    <span className="text-sm font-semibold text-fg truncate block">{ticket.user_email}</span>
                  </div>
                </div>

                <div className="flex gap-4">
                  <Calendar className="w-4 h-4 text-fg-subtle mt-1" />
                  <div>
                    <p className="block text-[10px] font-bold text-fg-subtle uppercase tracking-wider">Created Date</p>
                    <span className="text-sm font-semibold text-fg">
                      {new Date(ticket.created_at).toLocaleDateString(undefined, { dateStyle: 'long' })}
                    </span>
                  </div>
                </div>
              </div>

              <div className="mt-8 pt-6 border-t border-line-soft space-y-3">
                <div className="flex items-center gap-2 text-xs text-fg-subtle font-medium">
                  <BadgeCheck className="w-4 h-4 text-brand" /> Verified System Ticket
                </div>
              </div>
            </div>

            {/* Quick Tips or Policy Box */}
            <div className="p-5 rounded-xl border border-brand-line bg-brand-soft/30">
              <h4 className="text-xs font-bold text-brand uppercase tracking-wider mb-2 flex items-center gap-2">
                <div className="w-1.5 h-1.5 rounded-full bg-brand"></div>
                Admin Policy
              </h4>
              <p className="text-xs leading-relaxed text-brand opacity-80">
All responses are logged and archived. Users will receive an automated email notification once you submit the resolution.              </p>
            </div>
          </div>

        </div>
      </div>
    </div>
  );
}
