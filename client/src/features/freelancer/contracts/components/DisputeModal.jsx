import { useState } from "react";
import { useDisputeMutation } from "../contractMutations";
import { useDisputeReasons, reasonLabel } from "../../../disputes/disputeReasonQueries";
import { X, AlertTriangle, UploadCloud } from "lucide-react";
import Dialog from "../../../../components/common/Dialog";
import { Field } from "../../../../components/common/Field";

export default function DisputeModal({ onClose, contractId }) {
  const [reason, setReason] = useState("");
  // These two modals never had a validation error state. They relied on
  // `required` plus a disabled submit button, so the browser could still block
  // an empty submit and nothing was ever shown to the user explaining why.
  // <Field> needs something to render, so the state exists now -- and it is
  // wired into handleSubmit below rather than left as a stub.
  const [fieldError, setFieldError] = useState({});
  const [description, setDescription] = useState("");
  const [files, setFiles] = useState([]);
  // Dropped: the dropzone styles itself with `group-hover` only, so this state
  // was written by three drag handlers and read by nothing. The highlight-on-
  // drag behaviour it was presumably meant to drive never existed.
  const { mutate, isPending } = useDisputeMutation();
  const { data: reasons = [] } = useDisputeReasons();

  const handleSubmit = (e) => {
    e.preventDefault();
    const next = {};
    if (!reason) next.reason = "Choose a reason for the dispute.";
    if (!description.trim()) next.description = "Describe what went wrong.";
    setFieldError(next);
    if (Object.keys(next).length) return;

    mutate({
      contract: contractId,
      reason,
      description,
      files,
    }, {
      onSuccess: () => {
        onClose();
      }
    });
  };

  const handleFileSelect = (e) => {
    const selectedFiles = Array.from(e.target.files);
    setFiles((prev) => [...prev, ...selectedFiles]);
  };

  const removeFile = (index) => {
    setFiles(files.filter((_, i) => i !== index));
  };

  return (
    <Dialog
            open
            onClose={onClose}
      title="Raise a Dispute"
      size="md"
      className="overflow-hidden flex flex-col"
    >
        
        {/* Header */}
        <div className="flex items-center justify-between p-5 border-b border-line-soft">
          <div className="flex items-center gap-4">
            <div className="flex items-center justify-center w-11 h-11 bg-danger-soft rounded-2xl flex-shrink-0">
              <AlertTriangle className="w-6 h-6 text-danger" />
            </div>
            <div>
              <h1 className="text-lg font-black text-fg leading-none">Raise a Dispute</h1>
              <p className="text-[11px] text-fg-subtle font-bold uppercase tracking-wider mt-1">Order #ORD-{contractId}</p>
            </div>
          </div>
          <button onClick={onClose} className="p-2 hover:bg-surface-3 rounded-full transition-colors">
            <X className="w-5 h-5 text-fg-subtle" />
          </button>
        </div>

        <form onSubmit={handleSubmit} className="p-5 sm:p-8 overflow-y-auto space-y-6">
          {/* Dropdown Fix */}
          <div className="space-y-2">
            <Field label="Reason for Dispute" required error={fieldError.reason}>
              {(field) => (
                <div className="relative">
                  <select
                    value={reason}
                    onChange={(e) => setReason(e.target.value)}
                    {...field}
                    className="w-full h-[56px] px-5 text-sm font-bold text-fg bg-surface-2 border border-transparent rounded-2xl appearance-none focus-visible:bg-surface focus-visible:ring-4 focus-visible:ring-primary/10 focus-visible:border-primary outline-none transition-all cursor-pointer"
                    required
                  >
                    <option value="" className="text-fg-subtle">Select a reason</option>
                    {/* All 8 reasons, from GET /disputes/reasons. Was a hardcoded 4. */}
                    {reasons.map((option) => (
                      <option key={option.value} value={option.value}>
                        {reasonLabel(option, "freelancer")}
                      </option>
                    ))}
                  </select>
                  <div className="absolute right-5 top-1/2 -translate-y-1/2 pointer-events-none" aria-hidden="true">
                    <svg width="12" height="8" viewBox="0 0 12 8" fill="none" className="text-fg-subtle">
                      <path d="M1 1L6 6L11 1" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round"/>
                    </svg>
                  </div>
                </div>
              )}
            </Field>
          </div>

          {/* Description */}
          <div className="space-y-2">
            <Field label="Describe the Issue" required error={fieldError.description}>
              {(field) => (
                <textarea
                  value={description}
                  onChange={(e) => setDescription(e.target.value)}
                  placeholder="Explain the situation in detail..."
                  {...field}
                  className="w-full h-[140px] px-5 py-4 text-sm font-medium text-fg placeholder:text-fg-subtle bg-surface-2 border border-transparent rounded-2xl resize-none focus-visible:bg-surface focus-visible:ring-4 focus-visible:ring-primary/10 focus-visible:border-primary outline-none transition-all"
                  required
                />
              )}
            </Field>
          </div>

          {/* File Upload */}
          <div className="space-y-2">
            {/* "(optional)" was inside the label text, so it became part of the
                field's accessible name. It is a hint, not a name. */}
            <Field label="Supporting Files" hint="Optional">
              {(field) => (
                <>
                  <div className="relative group">
                    <input
                      type="file"
                      multiple
                      accept=".png,.jpg,.jpeg,.pdf,.mp4"
                      {...field}
                      className="absolute inset-0 w-full h-full opacity-0 cursor-pointer z-10"
                      onChange={handleFileSelect}
                    />
                    <div className="border-2 border-dashed border-line rounded-2xl p-6 flex flex-col items-center justify-center bg-surface-2 group-hover:bg-surface-3 transition-colors">
                      <UploadCloud className="w-8 h-8 text-fg-subtle mb-2" aria-hidden="true" />
                      <p className="text-xs font-bold text-fg-subtle" aria-hidden="true">
                        {files.length > 0 ? `${files.length} files selected` : "Tap to upload evidence"}
                      </p>
                    </div>
                  </div>

                  {files.length > 0 && (
                    <div className="flex flex-wrap gap-2 mt-3">
                      {files.map((file, i) => (
                        <div key={i} className="flex items-center gap-2 bg-surface-3 px-3 py-1.5 rounded-full text-[10px] font-bold text-fg-muted">
                          <span className="truncate max-w-[100px]">{file.name}</span>
                          {/* Announced only as "X" before. */}
                          <button
                            type="button"
                            onClick={() => removeFile(i)}
                            aria-label={`Remove ${file.name}`}
                            className="hover:text-danger-soft-fg"
                          >
                            <X className="w-3 h-3" aria-hidden="true" />
                          </button>
                        </div>
                      ))}
                    </div>
                  )}
                </>
              )}
            </Field>
          </div>

          {/* Actions */}
          <div className="flex flex-col sm:flex-row gap-3 pt-4">
            <button
              onClick={onClose}
              type="button"
              className="w-full sm:flex-1 h-14 px-6 text-xs font-black uppercase tracking-widest text-fg-subtle bg-surface border border-line rounded-2xl hover:bg-surface-2 transition-all active:scale-[0.98]"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={!reason || !description || isPending}
              className={`w-full sm:flex-1 h-14 px-6 text-xs font-black uppercase tracking-widest text-white rounded-2xl transition-all active:scale-[0.98] shadow-lg shadow-primary/20 flex items-center justify-center gap-2 ${
                isPending ? "bg-surface-inset cursor-not-allowed shadow-none" : "bg-brand hover:brightness-110"
              }`}
            >
              {isPending ? "Submitting..." : "Submit Dispute"}
            </button>
          </div>
        </form>
    </Dialog>
  );
}
