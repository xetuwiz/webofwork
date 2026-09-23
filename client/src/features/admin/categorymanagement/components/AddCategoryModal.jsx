import { useState } from "react";
import toast from "react-hot-toast";
import { useCreateCategory } from "../categoryMutations.js";

export function AddCategoryModal({ open, onOpenChange }) {
  const [name, setName] = useState("");
  const createMutation = useCreateCategory();

  const handleSubmit = (event) => {
    event.preventDefault();
    const trimmedName = name.trim();

    if (!trimmedName) {
      toast.error("Category name is required.");
      return;
    }

    createMutation.mutate(
      { name: trimmedName },
      {
        onSuccess: () => {
          setName("");
          toast.success("Category created.");
          onOpenChange(false);
        },
        onError: (error) => {
          const message = error?.response?.data?.error || error?.response?.data?.detail || "Failed to create category.";
          toast.error(message);
        },
      },
    );
  };

  if (!open) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/70 p-4">
      <div className="w-[440px] rounded-xl bg-white p-8 shadow-2xl">
        <h2 className="mb-4 text-lg font-semibold text-slate-800">Add Category</h2>

        <form onSubmit={handleSubmit} className="space-y-4">
          <input
            value={name}
            onChange={(event) => setName(event.target.value)}
            placeholder="Web Development"
            className="h-12 w-full rounded-lg border border-slate-200 px-4 text-slate-700 outline-none transition focus:border-blue-500 focus:ring-2 focus:ring-blue-100"
          />

          {createMutation.isError && (
            <p className="text-xs text-red-600">
              {createMutation.error?.response?.data?.error || createMutation.error?.response?.data?.detail || "Failed to create category."}
            </p>
          )}

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
              disabled={createMutation.isPending}
            >
              {createMutation.isPending ? "Adding..." : "Add"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
