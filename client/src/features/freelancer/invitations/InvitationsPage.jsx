import { useReceivedInvitations } from "./invitationQueries.js";
import { useUpdateInvitationStatus } from "./invitationMutations.js";

export default function InvitationsPage() {
  const { data, isLoading, isError } = useReceivedInvitations();
  const mutation = useUpdateInvitationStatus();
  const invitations = data?.results || data || [];

  if (isLoading) return <div className="p-8 text-center text-slate-500">Loading invitations…</div>;
  if (isError) return <div className="p-8 text-center text-red-600">Failed to load invitations.</div>;

  return (
    <div className="mx-auto max-w-5xl px-4 py-8 sm:px-6 lg:px-8">
      <div className="mb-8">
        <p className="text-sm font-semibold uppercase tracking-[0.22em] text-blue-600">Freelancer</p>
        <h1 className="mt-2 text-3xl font-bold text-slate-900">Invitations</h1>
      </div>

      {invitations.length === 0 ? (
        <div className="rounded-2xl border border-dashed border-slate-200 bg-slate-50 p-12 text-center text-slate-500">No invitations yet.</div>
      ) : (
        <div className="space-y-4">
          {invitations.map((invite) => (
            <article key={invite.id ?? invite.Id} className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
              <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
                <div>
                  <h2 className="text-lg font-semibold text-slate-900">{invite.projectTitle ?? invite.project?.title ?? invite.projectName ?? "Project invite"}</h2>
                  <p className="mt-1 text-sm text-slate-500">{invite.message ?? "You have been invited to this project."}</p>
                </div>
                <div className="flex gap-2">
                  <button type="button" onClick={() => mutation.mutate({ id: invite.id ?? invite.Id, status: "accepted" })} className="rounded-xl bg-emerald-600 px-3.5 py-2 text-sm font-semibold text-white hover:bg-emerald-700">Accept</button>
                  <button type="button" onClick={() => mutation.mutate({ id: invite.id ?? invite.Id, status: "declined" })} className="rounded-xl border border-slate-200 px-3.5 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50">Decline</button>
                </div>
              </div>
            </article>
          ))}
        </div>
      )}
    </div>
  );
}
