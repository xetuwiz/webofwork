/**
 * The admin's view of every AI run, and the manager approval queue.
 *
 * ## Why this is the second gate's home
 *
 * A run reaches `PendingManagerApproval` only from a `validate`-stage run, and
 * nothing but an admin can move it from there. So this list is not a convenience
 * view of some telemetry -- it is the only place the second of the two
 * human-in-the-loop gates can actually be operated.
 *
 * That is why the queue is a filter rather than a tab the admin has to remember
 * to open. The question an admin opens this screen with is almost never "show me
 * all of it"; it is "what is waiting on me", and a page that makes them look for
 * that is a page the gate gets skipped on.
 */

import React, { useState } from "react";
import { Link } from "react-router-dom";
import { AlertTriangle, ArrowRight, Filter, Loader2 } from "lucide-react";
import Button from "../../components/common/Button";
import { useAiWorkflows } from "./aiWorkflowQueries";
import { describeStatus } from "./workflowDisplay";

/**
 * The filters, in the order an admin is likely to use them.
 *
 * `needs_approval` is first and is not a server-side filter -- it is a client
 * filter over the current page. That is a real limitation and worth naming: a
 * queue that only searches what is loaded can show an empty queue while a
 * waiting run sits on page four. The page size is 25 and the table says so, so
 * the gap is visible rather than silent.
 */
const FILTERS = [
  { key: "needs_approval", label: "Needs approval" },
  { key: "all", label: "All" },
  { key: "talent_match", label: "Talent match" },
  { key: "validate", label: "Validate" },
  { key: "screen", label: "Screen" },
  { key: "draft", label: "Draft" },
];

/** The runs an admin can act on, from the enum on the server. */
const ACTIONABLE = new Set(["PendingManagerApproval", "PendingApproval"]);

export default function AiWorkflowMonitor() {
  const [filter, setFilter] = useState("needs_approval");
  const [page, setPage] = useState(1);

  const { data, isLoading, isError } = useAiWorkflows({ page, pageSize: 25 });

  const runs = data?.results ?? [];
  const shown = filter === "needs_approval" ? runs.filter((r) => ACTIONABLE.has(r.status)) : runs;
  const count = data?.count ?? 0;
  const pageSize = 25;
  const pages = Math.max(1, Math.ceil(count / pageSize));

  return (
    <div className="p-6 lg:p-8 space-y-6">
      <header className="flex flex-col lg:flex-row lg:items-end lg:justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold text-fg">AI workflows</h1>
          <p className="text-sm text-fg-muted mt-1 max-w-prose">
            Every run, newest first. A run in <strong>Pending manager approval</strong> is
            waiting on you: nothing has been created until you approve it.
          </p>
        </div>

        <div className="flex items-center gap-2 flex-wrap">
          <Filter size={14} className="text-fg-muted" aria-hidden="true" />
          {FILTERS.map((f) => (
            <button
              key={f.key}
              onClick={() => {
                setFilter(f.key);
                setPage(1);
              }}
              aria-pressed={filter === f.key}
              className={`px-3 py-1.5 text-xs font-semibold rounded-lg border transition-colors ${
                filter === f.key
                  ? "bg-brand text-brand-fg border-brand"
                  : "bg-surface text-fg-muted border-line hover:text-fg"
              }`}
            >
              {f.label}
            </button>
          ))}
        </div>
      </header>

      {isLoading && (
        <div className="py-16 text-center text-fg-muted text-sm flex items-center justify-center gap-2">
          <Loader2 size={18} className="animate-spin" aria-hidden="true" />
          Loading runs
        </div>
      )}

      {isError && (
        <div className="rounded-2xl border border-danger-line bg-danger-soft p-6">
          <h2 className="font-semibold text-danger-fg text-sm">Could not load the runs</h2>
          <p className="text-danger-soft-fg text-sm mt-1.5">
            Nothing has been approved or created. Try again.
          </p>
        </div>
      )}

      {!isLoading && !isError && shown.length === 0 && (
        <div className="rounded-2xl border border-line bg-surface p-10 text-center">
          <h2 className="font-semibold text-fg text-sm">
            {filter === "needs_approval" ? "Nothing is waiting on you" : "No runs match"}
          </h2>
          <p className="text-fg-muted text-sm mt-1.5">
            {filter === "needs_approval" && count > 0
              ? `Nothing on this page, but there are ${count} run${count === 1 ? "" : "s"} in total. Check the other pages.`
              : "Runs appear here as soon as a client or a trigger starts one."}
          </p>
        </div>
      )}

      {shown.length > 0 && (
        <ul className="space-y-3">
          {shown.map((run) => {
            const copy = describeStatus(run.status);
            const actionable = ACTIONABLE.has(run.status);

            return (
              <li key={run.id}>
                <Link
                  to={`/admin/ai-workflows/${run.id}`}
                  className="block bg-surface rounded-2xl border border-line p-5 hover:border-line-strong transition-colors"
                >
                  <div className="flex items-start gap-4">
                    {actionable && (
                      <AlertTriangle
                        size={18}
                        className="text-warning-fg shrink-0 mt-0.5"
                        aria-hidden="true"
                      />
                    )}

                    <div className="flex-1 min-w-0">
                      <div className="flex items-center gap-3 flex-wrap">
                        <span className="font-semibold text-fg text-sm truncate">
                          {run.objective || `Run #${run.id}`}
                        </span>
                        <span
                          className={`text-[10px] font-bold uppercase tracking-widest px-2 py-0.5 rounded ${
                            copy.tone === "danger"
                              ? "bg-danger-soft text-danger-fg"
                              : copy.tone === "warn"
                                ? "bg-warning-soft text-warning-fg"
                                : copy.tone === "ok"
                                  ? "bg-success-soft text-success-fg"
                                  : "bg-surface-2 text-fg-muted"
                          }`}
                        >
                          {copy.label}
                        </span>
                      </div>

                      <p className="text-xs text-fg-muted mt-1.5 flex items-center gap-3 flex-wrap">
                        <span>#{run.id}</span>
                        <span>stage: {run.stage}</span>
                        {/* snake_case, as the API sends it: `Program.cs` sets
                            `PropertyNamingPolicy = SnakeCaseLower` and there is no camelCase
                            transform on the axios response. Read as camelCase, `projectId`
                            was always undefined -- so no run ever showed which project it
                            belonged to -- and `createdAt` rendered as "Invalid Date". */}
                        {run.project_id && <span>project #{run.project_id}</span>}
                        {run.created_at && (
                          <span>{new Date(run.created_at).toLocaleString()}</span>
                        )}
                      </p>
                    </div>

                    <ArrowRight size={16} className="text-fg-muted shrink-0 mt-1" aria-hidden="true" />
                  </div>
                </Link>
              </li>
            );
          })}
        </ul>
      )}

      {count > pageSize && (
        <div className="flex items-center justify-between gap-3">
          <Button
            variant="ghost"
            size="sm"
            disabled={page <= 1}
            onClick={() => setPage((p) => Math.max(1, p - 1))}
          >
            Previous
          </Button>
          <span className="text-xs text-fg-muted tabular-nums">
            Page {page} of {pages}
          </span>
          <Button
            variant="ghost"
            size="sm"
            disabled={page >= pages}
            onClick={() => setPage((p) => Math.min(pages, p + 1))}
          >
            Next
          </Button>
        </div>
      )}
    </div>
  );
}
