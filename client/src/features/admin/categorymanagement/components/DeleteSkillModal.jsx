import { useState } from "react";
import toast from "react-hot-toast";
import { useDeleteSkill } from "../categoryMutations.js";

export default function DeleteSkillModal({ open, onOpenChange, skill }) {
  const deleteSkillMutation = useDeleteSkill();
  const [isSubmitting, setIsSubmitting] = useState(false);

  if (!open || !skill) return null;

  const handleDelete = () => {
    setIsSubmitting(true);
    deleteSkillMutation.mutate(skill.id, {
      onSuccess: () => {
        toast.success("Skill deleted.");
        onOpenChange(false);
      },
      onError: (error) => {
        const message = error?.response?.data?.error || error?.response?.data?.detail || "Failed to delete skill.";
        toast.error(message);
      },
      onSettled: () => setIsSubmitting(false),
    });
  };

  const handleCancel = () => {
    onOpenChange(false);
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 p-4">
      <div className="w-full max-w-[448px] overflow-hidden rounded-xl bg-white shadow-2xl">
        <div className="flex items-center justify-between px-8 pb-6 pt-8">
          <h2 className="text-[17px] font-semibold text-slate-800">Delete Skill</h2>
          <button type="button" onClick={handleCancel} className="text-slate-400 transition hover:text-slate-600">
            ✕
          </button>
        </div>

        <div className="px-8 pb-8">
          <p className="mb-4 text-xs text-slate-600">Are you sure you want to delete this skill?</p>

          <div className="mb-6 rounded-lg bg-slate-50 px-4 py-4">
            <p className="text-sm font-medium text-slate-800">{skill.name}</p>
          </div>

          <p className="mb-8 text-xs text-slate-500">This action cannot be undone.</p>

          <div className="flex gap-3">
            <button
              type="button"
              onClick={handleCancel}
              className="flex-1 rounded-lg bg-slate-100 py-3 font-medium text-slate-700 transition hover:bg-slate-200"
            >
              Cancel
            </button>
            <button
              type="button"
              onClick={handleDelete}
              disabled={deleteSkillMutation.isPending || isSubmitting}
              className="flex-1 rounded-lg bg-red-500 py-3 font-medium text-white transition hover:bg-red-600 disabled:cursor-not-allowed disabled:opacity-60"
            >
              {deleteSkillMutation.isPending || isSubmitting ? "Deleting..." : "Delete Skill"}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
