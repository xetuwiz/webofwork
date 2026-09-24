import { useForm } from "react-hook-form";
import toast from "react-hot-toast";
import { useEligibleProjects, useSendInvitation } from "../findTalentQueries.js";

export default function InvitationModal({ freelancer, isOpen, onClose }) {
  const { register, handleSubmit, reset } = useForm({ defaultValues: { projectId: "" } });
  const { data: eligibleProjectsData } = useEligibleProjects(Boolean(isOpen && freelancer));
  const eligibleProjects = eligibleProjectsData?.results || eligibleProjectsData || [];
  const mutation = useSendInvitation();

  if (!isOpen || !freelancer) return null;

  const onSubmit = (values) => {
    if (!values.projectId) {
      toast.error("Select a project before sending an invitation.");
      return;
    }

    mutation.mutate(
      {
        freelancerId: freelancer.id ?? freelancer.Id,
        projectId: Number(values.projectId),
      },
      {
        onSuccess: () => {
          toast.success("Invitation sent.");
          reset();
          onClose();
        },
        onError: (error) => {
          const message = error?.response?.data?.detail || error?.response?.data?.message || "Unable to send invitation.";
          toast.error(message);
        },
      }
    );
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 p-4 backdrop-blur-sm">
      <div className="w-full max-w-lg rounded-2xl bg-white p-6 shadow-xl">
        <div className="flex items-center justify-between gap-3">
          <div>
            <p className="text-sm font-semibold uppercase tracking-[0.2em] text-blue-600">Invite freelancer</p>
            <h3 className="mt-2 text-2xl font-bold text-slate-900">{freelancer.fullName || freelancer.full_name || freelancer.name}</h3>
          </div>
          <button type="button" onClick={onClose} className="rounded-full border border-slate-200 px-2.5 py-1.5 text-sm text-slate-500 hover:bg-slate-100">Close</button>
        </div>

        <form onSubmit={handleSubmit(onSubmit)} className="mt-6 space-y-5">
          <div>
            <label className="mb-2 block text-sm font-medium text-slate-700">Choose a project</label>
            <select {...register("projectId")} className="w-full rounded-xl border border-slate-200 bg-white px-3 py-2.5 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100">
              <option value="">Select a project</option>
              {(eligibleProjects || []).map((project) => (
                <option key={project.id ?? project.Id} value={project.id ?? project.Id}>
                  {project.title ?? project.Title}
                </option>
              ))}
            </select>
          </div>

          <div className="flex justify-end gap-3 pt-2">
            <button type="button" onClick={onClose} className="rounded-xl border border-slate-200 px-4 py-2.5 text-sm font-medium text-slate-700 hover:bg-slate-50">Cancel</button>
            <button type="submit" disabled={mutation.isPending} className="rounded-xl bg-blue-600 px-4 py-2.5 text-sm font-semibold text-white hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-60">
              {mutation.isPending ? "Sending..." : "Send invitation"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
