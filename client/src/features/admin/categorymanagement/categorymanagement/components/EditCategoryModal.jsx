import { useUpdateCategory } from "../categoryMutations";
import { useEffect, useState } from "react";
import Dialog from "../../../../components/common/Dialog";
import { toastSuccess } from "../../../../utils/toast";

export function EditCategoryModal({ open, onOpenChange, category }) {
  const [name, setName] = useState("");
  const updateMutation = useUpdateCategory();

useEffect(() => {
    if (open && category) {
      setName(category.name);
    }
  }, [open, category]);

  const handleSubmit = (e) => {
    e.preventDefault();

    updateMutation.mutate(
      { id: category.id,
        data:{name}},
      {
        onSuccess: () => onOpenChange(false),
      }
    );
    toastSuccess("Category updated")
  };

  if (!category) return null;

  return (
    <Dialog
            open={open}
            onClose={() => onOpenChange(false)}
      title="Edit Category"
      size="sm"
    >
          <h2 className="text-lg font-semibold mb-4">Edit Category</h2>

          <form onSubmit={handleSubmit} className="space-y-4">
            <input
              value={name}
              onChange={(e) => setName(e.target.value)}
              className="w-full h-12 px-4 border rounded-lg"
            />

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
              >
                Save
              </button>
            </div>
          </form>
    </Dialog>
  );
}

