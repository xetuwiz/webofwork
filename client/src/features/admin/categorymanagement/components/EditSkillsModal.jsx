import { useState } from "react";
import toast from "react-hot-toast";
import { useUpdateSkill } from "../categoryMutations.js";

export default function EditSkillModal({ open, onOpenChange, skill, categories = [] }) {
  const [name, setName] = useState(skill?.name ?? "");
  const [categoryId, setCategoryId] = useState(skill?.categoryId ?? skill?.category ?? "");
  const updateSkillMutation = useUpdateSkill();

  if (!open || !skill) return null;

  const handleSubmit = (event) => {
    event.preventDefault();
    const trimmedName = name.trim();

    if (!trimmedName || !categoryId) {
      toast.error("Please fill in all fields.");
      return;
    }

    updateSkillMutation.mutate(
      {
        id: skill.id,
        data: {
          name: trimmedName,
          categoryId: Number(categoryId),
          isCustom: Boolean(skill.isCustom),
        },
      },
      {
        onSuccess: () => {
          toast.success("Skill updated.");
          onOpenChange(false);
        },
        onError: (error) => {
          const message = error?.response?.data?.error || error?.response?.data?.detail || "Failed to update skill.";
          toast.error(message);
        },
      },
    );
  };

  const handleCancel = () => {
    onOpenChange(false);
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 p-4">
      <div className="w-full max-w-[448px] rounded-xl bg-white shadow-2xl">
        <div className="flex items-center justify-between px-8 pb-6 pt-8">
          <h2 className="text-[17px] font-semibold text-slate-800">Edit Skill</h2>
          <button type="button" onClick={handleCancel} className="text-slate-400 transition hover:text-slate-600">
            ✕
          </button>
        </div>

        <div className="px-8 pb-8">
          <p className="mb-8 text-xs text-slate-600">Update the skill details below.</p>

          <form onSubmit={handleSubmit} className="space-y-6">
            <div className="space-y-2">
              <label className="text-xs font-medium text-slate-700">Skill Name</label>
              <input
                value={name}
                onChange={(event) => setName(event.target.value)}
                className="h-[50px] w-full rounded-lg border border-slate-200 px-4 text-slate-700 outline-none transition focus:border-blue-500 focus:ring-2 focus:ring-blue-100"
              />
            </div>

            <div className="space-y-2">
              <label className="text-xs font-medium text-slate-700">Category</label>
              <select
                value={categoryId}
                onChange={(event) => setCategoryId(event.target.value)}
                className="h-12 w-full rounded-lg border border-slate-200 bg-white px-4 text-slate-700 outline-none transition focus:border-blue-500 focus:ring-2 focus:ring-blue-100"
              >
                <option value="">Select category</option>
                {categories.map((category) => (
                  <option key={category.id} value={category.id}>
                    {category.name}
                  </option>
                ))}
              </select>
            </div>

            <div className="flex gap-3 pt-2">
              <button
                type="button"
                onClick={handleCancel}
                className="flex-1 rounded-lg bg-slate-100 py-3 font-medium text-slate-700 transition hover:bg-slate-200"
              >
                Cancel
              </button>
              <button
                type="submit"
                disabled={updateSkillMutation.isPending}
                className="flex-1 rounded-lg bg-blue-600 py-3 font-medium text-white transition hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-60"
              >
                {updateSkillMutation.isPending ? "Saving..." : "Save Changes"}
              </button>
            </div>
          </form>
        </div>
      </div>
    </div>
  );
}
