import toast from "react-hot-toast";
import { useDeleteCategory } from "../categoryMutations.js";

export function DeleteCategoryModal({ onOpenChange, category }) {
  const deleteMutation = useDeleteCategory();

  const handleDelete = () => {
    if (!category) return;

    deleteMutation.mutate(category.id, {
      onSuccess: () => {
        toast.success("Category deleted.");
        onOpenChange(false);
      },
      onError: (error) => {
        const message = error?.response?.data?.error || error?.response?.data?.detail || "Failed to delete category.";
        toast.error(message);
      },
    });
  };

  if (!category) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/70 p-4">
      <div className="w-[440px] rounded-xl bg-white p-8 shadow-2xl">
        <h2 className="mb-4 text-lg font-semibold text-slate-800">Delete Category</h2>

        <p className="mb-6 text-sm text-slate-600">
          Are you sure you want to delete <span className="font-semibold text-slate-800">{category.name}</span>?
        </p>

        <div className="flex gap-3">
          <button
            type="button"
            onClick={() => onOpenChange(false)}
            className="flex-1 rounded-lg bg-slate-100 py-3 font-medium text-slate-700 transition hover:bg-slate-200"
          >
            Cancel
          </button>
          <button
            type="button"
            onClick={handleDelete}
            className="flex-1 rounded-lg bg-red-600 py-3 font-medium text-white transition hover:bg-red-700 disabled:cursor-not-allowed disabled:opacity-60"
            disabled={deleteMutation.isPending}
          >
            {deleteMutation.isPending ? "Deleting..." : "Delete"}
          </button>
        </div>
      </div>
    </div>
  );
}
