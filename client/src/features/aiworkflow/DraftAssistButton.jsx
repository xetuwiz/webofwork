import React, { useState } from "react";
import { Sparkles, Loader2, X, Check } from "lucide-react";
import Button from "../../components/common/Button";
import Dialog from "../../components/common/Dialog";
import { toastError } from "../../utils/toast";
import { useDraft, useRequestDraft } from "./aiWorkflowQueries";
import { formatDraftPrice } from "./draftPrice";

/**
 * "Help me draft" -- turns whatever the client has typed into a filled-in form.
 *
 * ## It fills the form, it does not submit it
 *
 * Nothing here calls the create-project endpoint. The draft populates fields the
 * client can edit, ignore or delete, and posting is a separate deliberate act
 * they take themselves.
 *
 * That is not caution about the API. It is that the draft contains
 * model-suggested category and skill ids, and a project posted with a wrong
 * category is a project no relevant freelancer ever sees. A human reading the
 * form is the cheapest available check on that, and the existing create endpoint
 * re-validates regardless.
 *
 * ## No cost unless the client asks
 *
 * The button is inert until pressed, and pressing is a request. Nothing calls
 * the agent on mount, so merely opening the page costs nothing.
 */
export default function DraftAssistButton({
  partialText,
  onApply,
  disabled = false,
  className = "",
}) {
  const [open, setOpen] = useState(false);
  const [runId, setRunId] = useState(null);
  const request = useRequestDraft();
  const draft = useDraft(runId, { enabled: Boolean(runId) });

  const begin = async () => {
    const text = (partialText ?? "").trim();
    if (text.length < 20) {
      // Said up front rather than by a failed request. Twenty characters is about
      // one sentence, below which there is nothing to draft from.
      toastError("Write a sentence or two first, so there is something to work from.");
      return;
    }

    setOpen(true);
    setRunId(null);

    try {
      const result = await request.mutateAsync({ partialText: text });
      setRunId(result?.workflow_run_id ?? null);
    } catch (error) {
      toastError(error?.message ?? "Could not start a draft.");
      setOpen(false);
    }
  };

  const close = () => {
    setOpen(false);
    setRunId(null);
  };

  const asking = draft.data?.clarification_question;
  const ready = Boolean(draft.data?.draft) && !draft.isLoading;

  return (
    <>
      <Button
        variant="ghost"
        onClick={begin}
        disabled={disabled}
        loading={request.isPending}
        className={className}
      >
        <Sparkles size={16} aria-hidden="true" className="mr-2" />
        Help me draft
      </Button>

      <Dialog
        open={open}
        onClose={close}
        title="Draft from your notes"
        description="Nothing is posted. You can change anything before publishing."
        size="lg"
      >
        <div className="space-y-5">
          {request.isPending && (
            <p className="text-sm text-fg-muted flex items-center gap-2">
              <Loader2 size={16} className="animate-spin" aria-hidden="true" />
              Reading your notes...
            </p>
          )}

          {ready && asking && (
            <div className="rounded-xl border border-line bg-surface-2 p-4">
              <h3 className="font-semibold text-fg text-sm">One question first</h3>
              <p className="text-fg-muted text-sm mt-1.5 leading-relaxed">{asking}</p>
              <p className="text-fg-subtle text-xs mt-3">
                Answer it in the form and press this again.
              </p>
            </div>
          )}

          {ready && !asking && (
            <>
              <div className="rounded-xl border border-success-line bg-success-soft p-4 flex gap-3">
                <Check size={18} className="text-success-fg shrink-0 mt-0.5" aria-hidden="true" />
                <p className="text-sm text-success-soft-fg leading-relaxed">
                  {draft.data.summary ??
                    "A draft is ready. Check every field before publishing -- the AI can get the category wrong, and the wrong category means the wrong people see your work."}
                </p>
              </div>

              <DraftPreview draft={draft.data.draft} />
            </>
          )}

          {draft.isError && (
            <p className="text-sm text-danger-fg">
              The draft could not be loaded. Close this and try again.
            </p>
          )}

          {!request.isPending && !ready && !draft.isError && (
            <p className="text-sm text-fg-muted flex items-center gap-2">
              <Loader2 size={16} className="animate-spin" aria-hidden="true" />
              Drafting...
            </p>
          )}

          <div className="flex justify-end gap-3">
            <Button variant="ghost" onClick={close}>
              Close
            </Button>
            {ready && !asking && onApply && (
              <Button variant="primary" onClick={() => { onApply(draft.data.draft); close(); }}>
                Fill the form
              </Button>
            )}
          </div>
        </div>
      </Dialog>
    </>
  );
}

/**
 * Renders the raw draft.
 *
 * The server returns the agent's output as a JSON string under `raw`, because the
 * contract between .NET and the agent service is deliberately loose -- a draft
 * that fails to parse should still reach the client as text to look at, not
 * vanish behind a 400. So it is parsed here, defensively, and anything
 * unparseable is shown as-is.
 */
function DraftPreview({ draft }) {
  const parsed = (() => {
    const raw = draft?.raw;
    if (!raw) return null;
    if (typeof raw === "object") return raw;
    try {
      return JSON.parse(raw);
    } catch {
      return null;
    }
  })();

  if (!parsed) {
    return (
      <pre className="text-xs text-fg-muted bg-surface-2 rounded-xl p-4 overflow-x-auto whitespace-pre-wrap">
        {String(draft?.raw ?? "")}
      </pre>
    );
  }

  // Lives in `draftPrice.js` so the range logic is testable on its own rather
  // than only through a rendered dialog. See that file for what it got wrong.
  const price = formatDraftPrice(parsed);

  const fields = [
    ["Title", parsed.title],
    ["Category", parsed.category_name ?? parsed.category_id],
    ["Skills", Array.isArray(parsed.skill_names) ? parsed.skill_names.join(", ") : null],
    ["Pricing", parsed.pricing_type],
    ["Price", price],
    ["Delivery", parsed.delivery_days ? `${parsed.delivery_days} days` : null],
  ].filter(([, value]) => value !== null && value !== undefined && value !== "");

  return (
    <div className="space-y-4">
      <dl className="grid sm:grid-cols-2 gap-x-6 gap-y-3">
        {fields.map(([label, value]) => (
          <div key={label}>
            <dt className="text-[10px] font-bold uppercase tracking-widest text-fg-subtle">
              {label}
            </dt>
            <dd className="text-sm text-fg mt-0.5 break-words">{String(value)}</dd>
          </div>
        ))}
      </dl>

      {parsed.description && (
        <div>
          <h4 className="text-[10px] font-bold uppercase tracking-widest text-fg-subtle mb-1.5">
            Description
          </h4>
          <p className="text-sm text-fg-muted leading-relaxed whitespace-pre-wrap">
            {parsed.description}
          </p>
        </div>
      )}

      {Array.isArray(parsed.notes) && parsed.notes.length > 0 && (
        <ul className="space-y-1.5">
          {parsed.notes.map((note) => (
            <li key={note} className="text-xs text-fg-subtle flex gap-1.5">
              <X size={12} className="shrink-0 mt-0.5" aria-hidden="true" />
              {note}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
