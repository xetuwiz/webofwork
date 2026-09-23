import { useState } from "react";
import toast from "react-hot-toast";
import { useUpdateCategory } from "../categoryMutations.js";

export function EditCategoryModal({ onOpenChange, category }) {
  const [name, setName] = useState(category?.name ?? "");
  const updateMutation = useUpdateCategory();

  const handleSubmit = (event) => {
    event.preventDefault();
    const trimmedName = name.trim();

    if (!category || !trimmedName) {
      toast.error("Category name is required.");
      return;
    }

    updateMutation.mutate(
      { id: category.id, data: { name: trimmedName } },
      {
        onSuccess: () => {
          toast.success("Category updated.");
          onOpenChange(false);
        },
        onError: (error) => {
          const message = error?.response?.data?.error || error?.response?.data?.detail || "Failed to update category.";
          toast.error(message);
        },
      },
    );
  };

  if (!category) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/70 p-4">
      <div className="w-[440px] rounded-xl bg-white p-8 shadow-2xl">
        <h2 className="mb-4 text-lg font-semibold text-slate-800">Edit Category</h2>

        <form onSubmit={handleSubmit} className="space-y-4">
          <input
            value={name}
            onChange={(event) => setName(event.target.value)}
            className="h-12 w-full rounded-lg border border-slate-200 px-4 text-slate-700 outline-none transition focus:border-blue-500 focus:ring-2 focus:ring-blue-100"
          />

          <div className="flex gap-3">
            <button
              type="button"
              onClick={() => onOpenChange(false)}
              className="flex-1 rounded-lg bg-slate-100 py-3 font-medium text-slate-700 transition hover:bg-slate-200"
            >
              Cancel
            </button>
            <button
              type="submit"
              className="flex-1 rounded-lg bg-blue-600 py-3 font-medium text-white transition hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-60"
              disabled={updateMutation.isPending}
            >
              {updateMutation.isPending ? "Saving..." : "Save"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
