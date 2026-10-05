import React, { useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { Check, Loader2, AlertTriangle, Send, Info, ExternalLink } from "lucide-react";
import Button from "../../components/common/Button";
import Dialog from "../../components/common/Dialog";
import { toastSuccess, toastError } from "../../utils/toast";
import { useApproveShortlist } from "./aiWorkflowQueries";
import { canApproveShortlist, formatScore, hasShortlist } from "./workflowDisplay";

 /**
 * The id that identifies a *person* on a recommendation row.
 *
 * `AgentRecommendation.FreelancerId` is the freelancer_profiles id, and the
 * approval endpoint stores what the client sends as
 * `ShortlistApproval.FreelancerIds` and hands them to the invitation service. So
 * this must be the profile id.
 *
 * It was `row.freelancerId ?? row.id`. There is no camelCase transform on the
 * axios response, so `row.freelancerId` was always undefined and the fallback
 * silently sent the *recommendation row id* instead -- inviting nobody, while the
 * client reported success. Silent because a wrong id is still a valid Guid.
 */
const freelancerIdOf = (row) => row.freelancer_id ?? row.freelancerId ?? null;

/**
 * How many reviews a candidate has, tolerating either casing.
 *
 * Zero reads as "no reviews" rather than as a count, and the caller omits it
 * entirely at zero -- "0 reviews" is a judgement, not a fact about the data.
 */
const reviewCountOf = (row) => {
  const value = row.review_count ?? row.reviewCount;
  return typeof value === "number" && value > 0 ? value : 0;
};

/**
 * The client gate: review a shortlist and invite the people you choose.
 *
 * ## Why the invites are behind a confirmation
 *
 * Every control in the row is inert until the run says it is waiting on this
 * client. That is not a safety flourish -- the server would refuse an approval
 * otherwise -- it is so the screen cannot present a button that is guaranteed to
 * fail. Someone who has read a shortlist and pressed "invite" deserves either an
 * invitation or an explanation, and a 400 saying "not awaiting approval" is
 * neither.
 *
 * ## Partial success is normal and is reported as such
 *
 * A shortlist assembled from people who applied will contain people who have
 * already proposed, and the server refuses to invite those. Ten approved, two
 * skipped is a good outcome, so the confirmation lists what was skipped and why
 * rather than presenting it as a failure.
 */
/**
 * The candidate's name, linked to the profile the client can actually open.
 *
 * Split out because it carries the fallback, and the fallback is a decision:
 * when the server sends no name the row says so plainly instead of asserting
 * "Freelancer". A placeholder that reads like a title is worse than an
 * admission, because it looks like data.
 */
function CandidateName({ row }) {
  const id = freelancerIdOf(row) ?? row.id;
  const name = row.freelancer_name ?? row.freelancerName;

  if (!name) {
    return (
      <span className="font-semibold text-fg-muted text-sm italic">
        Name unavailable
      </span>
    );
  }

  if (!id) return <span className="font-semibold text-fg text-sm">{name}</span>;

  return (
    <Link
      to={`/find-talents/${id}`}
      className="font-semibold text-fg text-sm hover:text-brand hover:underline inline-flex items-center gap-1.5"
    >
      {name}
      <ExternalLink size={13} className="shrink-0" aria-hidden="true" />
      <span className="sr-only">(opens their profile in a new page)</span>
    </Link>
  );
}

export default function ShortlistApprovalPanel({ workflowRunId, status, shortlist = [] }) {
  const approve = useApproveShortlist();
  const [selected, setSelected] = useState(() => new Set());
  const [confirming, setConfirming] = useState(false);

  const awaiting = canApproveShortlist(status) && hasShortlist(shortlist);
  const ranked = useMemo(
    () => [...shortlist].sort((a, b) => (b.score ?? 0) - (a.score ?? 0)),
    [shortlist]
  );

  const toggle = (id) =>
    setSelected((current) => {
      const next = new Set(current);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });

  const selectAll = () =>
    setSelected((current) =>
      current.size === ranked.length
        ? new Set()
        : new Set(ranked.map(freelancerIdOf).filter(Boolean))
    );

  const submit = async () => {
    try {
      const result = await approve.mutateAsync({
        id: workflowRunId,
        freelancerIds: [...selected],
      });

      // `invitation_ids`, because that is the key on the wire. The response
      // projects `invitation_ids = result.InvitationIds`, and `Program.cs`
      // applies `PropertyNamingPolicy = SnakeCaseLower`. Read as `invitationIds`
      // it was always undefined, so a successful approval of four people reported
      // "Invited 0 people" -- the invitations were real, and the screen said they
      // were not. A count that is wrong about a contact action is worse than no
      // count, so this tolerates both spellings rather than trusting one.
      const invited =
        result?.invitation_ids?.length ?? result?.invitationIds?.length ?? 0;
      const skipped = result?.skipped ?? [];

      if (skipped.length > 0) {
        // Warn rather than celebrate. The client asked to contact people and one
        // group of them was not contacted, which they need to know before they
        // tell a client "we've reached out".
        toastSuccess(
          `Invited ${invited}. ${skipped.length} could not be invited: ` +
            skipped.map((s) => s.reason).join("; ")
        );
      } else {
        toastSuccess(`Invited ${invited} ${invited === 1 ? "person" : "people"}.`);
      }

      setSelected(new Set());
      setConfirming(false);
    } catch (error) {
      toastError(error?.message ?? "Could not approve the shortlist.");
      setConfirming(false);
    }
  };

  return (
    <section className="bg-surface rounded-2xl border border-line overflow-hidden">
      <header className="p-6 sm:p-8 border-b border-line-soft">
        <h2 className="font-bold text-fg">Suggested freelancers</h2>
        <p className="text-fg-muted text-sm mt-1">
          {awaiting
            ? "Nobody has been contacted yet. Choose who to invite."
            : hasShortlist(shortlist)
              ? "These are suggestions. Invitations are sent once the AI has finished checking them."
              : "No suggestions yet."}
        </p>
      </header>

      {hasShortlist(shortlist) && (
        <div className="px-6 sm:px-8 py-3 bg-surface-2 border-b border-line-soft flex items-center justify-between">
          <label className="flex items-center gap-2 text-sm text-fg cursor-pointer">
            <input
              type="checkbox"
              checked={selected.size === ranked.length && ranked.length > 0}
              onChange={selectAll}
              disabled={!awaiting}
              className="h-4 w-4 rounded border-line-strong accent-[var(--brand)] disabled:opacity-50"
            />
            Select all ({ranked.length})
          </label>
          {awaiting && (
            <span className="text-sm font-semibold text-fg-muted">
              {selected.size} selected
            </span>
          )}
        </div>
      )}

      <ul className="divide-y divide-line-soft">
        {ranked.map((row) => {
          const id = freelancerIdOf(row) ?? row.id;
          const chosen = selected.has(id);
          return (
            <li key={id} className="p-6 sm:p-8">
              <div className="flex items-start gap-4">
                {awaiting && (
                  <input
                    type="checkbox"
                    checked={chosen}
                    onChange={() => toggle(id)}
                        /* Named, not generic. Three checkboxes all reading
                        "Invite this freelancer" are indistinguishable to a screen
                        reader, which makes the one decision on this page -- who gets
                        contacted -- unverifiable. Falls back to the old wording when
                        the server sent no name. */
                    aria-label={`Invite ${row.freelancer_name ?? "this freelancer"}`}
                    className="mt-1 h-5 w-5 rounded border-line-strong accent-[var(--brand)] shrink-0"
                  />
                )}

                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-3 flex-wrap">
                                        <CandidateName row={row} />

                    <span className="text-xs font-bold text-brand bg-brand-soft px-2 py-0.5 rounded-full">
                      {formatScore(row.score)} match
                    </span>

                                        {reviewCountOf(row) > 0 && (
                      <span className="text-xs text-fg-muted">
                        {reviewCountOf(row)} review{reviewCountOf(row) === 1 ? "" : "s"}
                      </span>
                    )}
                  </div>

                  {row.tagline && (
                    <p className="text-fg-muted text-sm mt-1">{row.tagline}</p>
                  )}

                  {row.reason && (
                    <p className="text-fg-muted text-sm mt-2 leading-relaxed">{row.reason}</p>
                  )}

                  {(row.strengths?.length > 0 || row.gaps?.length > 0) && (
                    <div className="mt-3 grid sm:grid-cols-2 gap-4">
                      {row.strengths?.length > 0 && (
                        <div>
                          <h4 className="text-[10px] font-bold uppercase tracking-widest text-success-fg mb-1.5">
                            Strengths
                          </h4>
                          <ul className="space-y-1">
                            {row.strengths.map((s) => (
                              <li key={s} className="text-sm text-fg-muted flex gap-1.5">
                                <Check size={14} className="text-success-fg shrink-0 mt-0.5" aria-hidden="true" />
                                {s}
                              </li>
                            ))}
                          </ul>
                        </div>
                      )}
                      {row.gaps?.length > 0 && (
                        <div>
                          <h4 className="text-[10px] font-bold uppercase tracking-widest text-warning-fg mb-1.5">
                            Gaps
                          </h4>
                          <ul className="space-y-1">
                            {row.gaps.map((g) => (
                              <li key={g} className="text-sm text-fg-muted flex gap-1.5">
                                <AlertTriangle size={14} className="text-warning-fg shrink-0 mt-0.5" aria-hidden="true" />
                                {g}
                              </li>
                            ))}
                          </ul>
                        </div>
                      )}
                    </div>
                  )}
                </div>
              </div>
            </li>
          );
        })}
      </ul>

      {awaiting && (
        <footer className="p-6 sm:p-8 border-t border-line-soft bg-surface-2">
          <div className="flex flex-col sm:flex-row sm:items-center gap-3">
            <Button
              variant="primary"
              onClick={() => setConfirming(true)}
              disabled={selected.size === 0 || approve.isPending}
              loading={approve.isPending}
            >
              <Send size={16} aria-hidden="true" className="mr-2" />
              {selected.size > 0 ? `Invite ${selected.size} ` : "Invite "}{selected.size === 1 ? "person" : "people"}
            </Button>
            <p className="text-xs text-fg-muted flex items-start gap-1.5">
              <Info size={14} className="shrink-0 mt-0.5" aria-hidden="true" />
              This sends invitations from your account. You can cancel the workflow
              instead if you would rather start over.
            </p>
          </div>
        </footer>
      )}

      <Dialog
        open={confirming}
        onClose={() => setConfirming(false)}
        title={`Invite ${selected.size} ${selected.size === 1 ? "person" : "people"}?`}
        size="sm"
      >
        <div className="space-y-4">
          <p className="text-sm text-fg-muted leading-relaxed">
            They will be invited to this project and notified. Anyone who has already
            applied will be skipped rather than contacted twice.
          </p>
          <div className="flex justify-end gap-3">
            <Button variant="ghost" onClick={() => setConfirming(false)} disabled={approve.isPending}>
              Cancel
            </Button>
            <Button variant="primary" onClick={submit} loading={approve.isPending}>
              {approve.isPending ? (
                <>
                  <Loader2 size={16} className="mr-2 animate-spin" aria-hidden="true" />
                  Sending
                </>
              ) : (
                <>
                  <Send size={16} className="mr-2" aria-hidden="true" />
                  Send invitations
                </>
              )}
            </Button>
          </div>
        </div>
      </Dialog>
    </section>
  );
}
