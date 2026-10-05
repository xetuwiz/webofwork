import { useState } from "react";
import { useDisputeMutation } from "../../../freelancer/contracts/contractMutations";
import { useDisputeReasons, reasonLabel } from "../../../disputes/disputeReasonQueries";
import Dialog from "../../../../components/common/Dialog";
import { Field } from "../../../../components/common/Field";

export default function ClientDisputeModal({ onClose, contractId }) {
  const [reason, setReason] = useState("");
  // These two modals never had a validation error state. They relied on
  // `required` plus a disabled submit button, so the browser could still block
  // an empty submit and nothing was ever shown to the user explaining why.
  // <Field> needs something to render, so the state exists now -- and it is
  // wired into handleSubmit below rather than left as a stub.
  const [fieldError, setFieldError] = useState({});
  const [description, setDescription] = useState("");
  const [files, setFiles] = useState([]);
  const [isDragging, setIsDragging] = useState(false);

  const { mutate, isPending } = useDisputeMutation();
  const { data: reasons = [] } = useDisputeReasons();

  const handleSubmit = (e) => {
    e.preventDefault();
    const next = {};
    if (!reason) next.reason = "Choose a reason for the dispute.";
    if (!description.trim()) next.description = "Describe the issue.";
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

  const handleDragOver = (e) => {
    e.preventDefault();
    setIsDragging(true);
  };

  const handleDragLeave = () => {
    setIsDragging(false);
  };

  const handleDrop = (e) => {
    e.preventDefault();
    setIsDragging(false);
    const droppedFiles = Array.from(e.dataTransfer.files);
    setFiles((prev) => [...prev, ...droppedFiles]);
  };

  const handleFileSelect = (e) => {
    const selectedFiles = Array.from(e.target.files);
    setFiles((prev) => [...prev, ...selectedFiles]);
  };

  return (
    <Dialog
            open
            onClose={onClose}
      title="Raise a Dispute"
      size="lg"
      className="overflow-hidden"
    >
        <div className="flex items-start gap-3 p-5 sm:px-6 sm:py-5 border-b border-line-soft">
          <div className="flex-1 flex items-start gap-3">
            <div className="flex items-center justify-center w-10 h-10 bg-danger-soft rounded-lg flex-shrink-0">
              <svg width="20" height="20" viewBox="0 0 20 20" fill="none" xmlns="http://www.w3.org/2000/svg">
                <path d="M18.1083 14.9999L11.4416 3.33319C11.2962 3.0767 11.0854 2.86335 10.8307 2.71492C10.576 2.56649 10.2864 2.48828 9.99161 2.48828C9.69678 2.48828 9.40724 2.56649 9.1525 2.71492C8.89777 2.86335 8.68697 3.0767 8.54161 3.33319L1.87494 14.9999C1.72801 15.2543 1.65096 15.5431 1.65162 15.837C1.65227 16.1308 1.73059 16.4192 1.87865 16.673C2.0267 16.9269 2.23923 17.137 2.49469 17.2822C2.75014 17.4274 3.03945 17.5025 3.33327 17.4999H16.6666C16.959 17.4996 17.2462 17.4223 17.4993 17.2759C17.7525 17.1295 17.9626 16.9191 18.1087 16.6658C18.2548 16.4125 18.3316 16.1252 18.3316 15.8328C18.3315 15.5404 18.2545 15.2531 18.1083 14.9999Z" stroke="var(--danger)" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
                <path d="M10 7.5V10.8333" stroke="var(--danger)" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
                <path d="M10 14.167H10.0083" stroke="var(--danger)" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
              </svg>
            </div>
            <div className="flex-1 min-w-0">
              <h1 className="text-[17px] font-bold text-fg leading-7">Raise a Dispute</h1>
              <p className="text-xs text-fg-subtle leading-5 mt-1">Report an issue with this contract as a client for our team to review.</p>
              <p className="text-[10px] text-fg-subtle leading-4 mt-0.5">Order #ORD-{contractId}</p>
            </div>
          </div>
        </div>

        <form onSubmit={handleSubmit} className="p-6 space-y-6 overflow-y-auto max-h-[80vh]">
          <div className="space-y-2">
            <Field label="Reason for Dispute" required error={fieldError.reason}>
              {(field) => (
                <div className="relative">
                  <select
                    value={reason}
                    onChange={(e) => setReason(e.target.value)}
                    {...field}
                    className="w-full h-[50px] px-[17px] text-sm text-fg bg-surface border border-line rounded-lg appearance-none outline-none focus-visible:ring-2 focus-visible:ring-fg focus-visible:border-transparent"
                    required
                  >
                    <option value="">Select a reason</option>
                    {/* All 8 reasons, from GET /disputes/reasons. Was a hardcoded 6. */}
                    {reasons.map((option) => (
                      <option key={option.value} value={option.value}>
                        {reasonLabel(option, "client")}
                      </option>
                    ))}
                  </select>
                  <div className="absolute right-[17px] top-1/2 -translate-y-1/2 pointer-events-none" aria-hidden="true">
                    <svg width="20" height="20" viewBox="0 0 20 20" fill="none" xmlns="http://www.w3.org/2000/svg">
                      <path d="M5 7.5L10 12.5L15 7.5" stroke="var(--fg-subtle)" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
                    </svg>
                  </div>
                </div>
              )}
            </Field>
          </div>

          <div className="space-y-2">
            <Field label="Describe the Issue" required error={fieldError.description}>
              {(field) => (
                <textarea
                  value={description}
                  onChange={(e) => setDescription(e.target.value)}
                  placeholder="Provide details about the work quality, deadlines, or communication issues..."
                  {...field}
                  className="w-full h-[170px] px-[17px] py-[13px] text-sm text-fg placeholder:text-fg-subtle bg-surface border border-line rounded-lg resize-none outline-none focus-visible:ring-2 focus-visible:ring-fg focus-visible:border-transparent"
                  required
                />
              )}
            </Field>
          </div>

          <div className="space-y-2">
            {/* "(optional)" was inside the label text, so it became part of the
                field's accessible name. It is a hint, not a name. */}
            <Field label="Supporting Files" hint="Optional">
              {(field) => (
                <>
                  <div
                    onDragOver={handleDragOver}
                    onDragLeave={handleDragLeave}
                    onDrop={handleDrop}
                    className={`relative border-2 border-dashed ${
                      isDragging ? "border-inverse-fg/20 bg-surface-2" : "border-line"
                    } rounded-lg p-6 text-center transition-colors cursor-pointer hover:border-line-strong`}
                  >
                    <input
                      type="file"
                      multiple
                      accept=".png,.jpg,.jpeg,.pdf,.mp4"
                      {...field}
                      className="absolute inset-0 w-full h-full opacity-0 cursor-pointer"
                      onChange={handleFileSelect}
                    />
                    <div className="flex flex-col items-center justify-center space-y-2">
                      <div className="w-10 h-10 flex items-center justify-center bg-surface-2 rounded-full" aria-hidden="true">
                        <svg width="24" height="24" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
                          <path d="M12 3V15" stroke="var(--fg-subtle)" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
                          <path d="M17 8L12 3L7 8" stroke="var(--fg-subtle)" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
                          <path d="M21 15V19C21 19.5304 20.7893 20.0391 20.4142 20.4142C20.0391 20.7893 19.5304 21 19 21H5C4.46957 21 3.96086 20.7893 3.58579 20.4142C3.21071 20.0391 3 19.5304 3 19V15" stroke="var(--fg-subtle)" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
                        </svg>
                      </div>
                      <div>
                        <p className="text-xs font-medium text-fg-muted leading-5" aria-hidden="true">
                          {files.length > 0 ? `${files.length} files selected` : "Click to upload or drag and drop"}
                        </p>
                        <p className="text-[10px] text-fg-subtle leading-4 mt-1" aria-hidden="true">PNG, JPG, PDF, MP4 supported</p>
                      </div>
                    </div>
                  </div>
                  {files.length > 0 && (
                    <div className="mt-2 flex flex-wrap gap-2">
                      {files.map((file, i) => (
                        <span key={i} className="text-[10px] bg-surface-3 px-2 py-1 rounded-md text-fg-muted">
                          {file.name}
                        </span>
                      ))}
                    </div>
                  )}
                </>
              )}
            </Field>
          </div>

          <div className="flex flex-col sm:flex-row gap-3 pt-2">
            <button
              onClick={onClose}
              type="button"
              className="flex-1 h-12 px-6 shrink-0 min-h-10 text-sm font-medium text-fg bg-surface border border-line-strong rounded-lg hover:bg-surface-2 transition-colors"
            >
              Cancel
            </button>
            <button
              type="submit"
              className={`flex-1 h-12 px-6 shrink-0 min-h-10 text-sm font-medium text-white rounded-lg transition-colors ${
                isPending ? "bg-surface-inset cursor-not-allowed" : "bg-black hover:bg-inverse"
              }`}
              disabled={!reason || !description || isPending}
            >
              {isPending ? "Raising Dispute..." : "Submit Dispute"}
            </button>
          </div>
        </form>
    </Dialog>
  );
}
