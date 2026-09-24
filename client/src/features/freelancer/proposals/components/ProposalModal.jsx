import { useForm } from "react-hook-form";
import toast from "react-hot-toast";
import { useSubmitProposal } from "../proposalMutations.js";

export default function ProposalModal({ projectId, isOpen, onClose }) {
  const { register, handleSubmit, reset } = useForm({ defaultValues: { proposalText: "", coverLetter: "", bidAmount: "" } });
  const mutation = useSubmitProposal();

  if (!isOpen) return null;

  const onSubmit = (values) => {
    mutation.mutate(
      {
        projectId: Number(projectId),
        proposalText: values.proposalText,
        coverLetter: values.coverLetter,
        bidAmount: Number(values.bidAmount || 0),
      },
      {
        onSuccess: () => {
          toast.success("Proposal sent.");
          reset();
          onClose();
        },
        onError: (error) => {
          const message = error?.response?.data?.detail || error?.response?.data?.message || "Unable to submit proposal.";
          toast.error(message);
        },
      }
    );
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 p-4 backdrop-blur-sm">
      <div className="w-full max-w-xl rounded-2xl bg-white p-6 shadow-xl">
        <div className="flex items-center justify-between gap-3">
          <div>
            <p className="text-sm font-semibold uppercase tracking-[0.2em] text-blue-600">Proposal</p>
            <h3 className="mt-2 text-2xl font-bold text-slate-900">Submit your proposal</h3>
          </div>
          <button type="button" onClick={onClose} className="rounded-full border border-slate-200 px-2.5 py-1.5 text-sm text-slate-500 hover:bg-slate-100">Close</button>
        </div>

        <form onSubmit={handleSubmit(onSubmit)} className="mt-6 space-y-5">
          <div>
            <label className="mb-2 block text-sm font-medium text-slate-700">Bid amount</label>
            <input type="number" min="1" step="0.01" {...register("bidAmount")} className="w-full rounded-xl border border-slate-200 px-3 py-2.5 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100" />
          </div>

          <div>
            <label className="mb-2 block text-sm font-medium text-slate-700">Cover letter</label>
            <textarea rows={4} {...register("coverLetter")} className="w-full rounded-xl border border-slate-200 px-3 py-2.5 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100" />
          </div>

          <div>
            <label className="mb-2 block text-sm font-medium text-slate-700">Proposal</label>
            <textarea rows={5} {...register("proposalText")} className="w-full rounded-xl border border-slate-200 px-3 py-2.5 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100" />
          </div>

          <div className="flex justify-end gap-3 pt-2">
            <button type="button" onClick={onClose} className="rounded-xl border border-slate-200 px-4 py-2.5 text-sm font-medium text-slate-700 hover:bg-slate-50">Cancel</button>
            <button type="submit" disabled={mutation.isPending} className="rounded-xl bg-blue-600 px-4 py-2.5 text-sm font-semibold text-white hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-60">
              {mutation.isPending ? "Submitting..." : "Send proposal"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
