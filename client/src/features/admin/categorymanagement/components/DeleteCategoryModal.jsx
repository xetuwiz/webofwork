import { useDeleteCategory } from "../categoryMutations";
import Dialog from "../../../../components/common/Dialog";
import { toastError } from "../../../../utils/toast";
import { getErrorMessage } from "../../../../utils/errorMessage";

export function DeleteCategoryModal({ open, onOpenChange, category }) {
  const deleteMutation = useDeleteCategory();

  const handleDelete = () => {
    deleteMutation.mutate(category.id, {
      onSuccess: () => onOpenChange(false),
      onError: (error) => {
        // This raised the failure text through `toastSuccess`. So deleting a
        // category that could not be deleted showed the reason in a *green
        // success toast* -- reading as "An error occurred: done", which is worse
        // than showing nothing because it looks like the operation succeeded.
        toastError(getErrorMessage(error, "An error occurred"));
      }
    });
  };

  if (!category) return null;

  return (
    <Dialog
            open={open}
            onClose={() => onOpenChange(false)}
      title="Delete Category"
      size="sm"
    >
          <h2 className="text-lg font-semibold mb-4">Delete Category</h2>

          <p className="text-sm text-fg-muted mb-6">
            Are you sure you want to delete <b>{category.name}</b>?
          </p>

          <div className="flex gap-3">
            <button
              onClick={() => onOpenChange(false)}
              className="flex-1 h-12 bg-surface-3 rounded-lg"
            >
              Cancel
            </button>
            <button
              onClick={handleDelete}
              className="flex-1 h-12 bg-danger text-danger-fg rounded-lg"
            >
              Delete
            </button>
          </div>
    </Dialog>
  );
}

