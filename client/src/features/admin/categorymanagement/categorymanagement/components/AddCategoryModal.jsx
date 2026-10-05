import * as React from "react";
import { useCreateCategory } from "../categoryMutations";
import Dialog from "../../../../components/common/Dialog";
import { toastSuccess } from "../../../../utils/toast";
import { getErrorMessage } from "../../../../utils/errorMessage";

export function AddCategoryModal({ open, onOpenChange }) {
  const [name, setName] = React.useState("");
  const createMutation = useCreateCategory();

  const handleSubmit = (e) => {
    e.preventDefault();

    createMutation.mutate(
      { name },
      {
        onSuccess: () => {
          setName("");
          onOpenChange(false);
        },
      }
    );
    toastSuccess("Category created")
  };

  return (
    <Dialog
            open={open}
            onClose={() => onOpenChange(false)}
      title="Add Category"
      size="sm"
    >
          <h2 className="text-lg font-semibold mb-4">Add Category</h2>

          <form onSubmit={handleSubmit} className="space-y-4">
            <input
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="Web Development"
              className="w-full h-12 px-4 border rounded-lg"
            />

            {createMutation.isError && (
              <p className="text-xs text-danger">
                {getErrorMessage(createMutation.error, "Failed to create category")}
              </p>
            )}

            <div className="flex gap-3">
              <button
                type="button"
                onClick={() => onOpenChange(false)}
                className="flex-1 h-12 bg-surface-3 rounded-lg"
              >
                Cancel
              </button>
              <button
                type="submit"
                className="flex-1 h-12 bg-brand text-brand-fg rounded-lg"
                disabled={createMutation.isLoading}
              >
                Add
              </button>
            </div>
          </form>
    </Dialog>
  );
}

