import { useState } from "react";
import { useCreateSkill } from "../skillMutations";
import { Loader2 } from "lucide-react";
import Dialog from "../../../../components/common/Dialog";
import { toastError, toastSuccess } from "../../../../utils/toast";
import Button from "../../../../components/common/Button";
import { Field } from "../../../../components/common/Field";

export default function AddSkillModal({ categories, isLoading, onClose }) {
  const [name, setName] = useState("");
  const [categoryId, setCategoryId] = useState("");
  const createSkillMutation = useCreateSkill();

  const handleSubmit = (e) => {
    e.preventDefault();
    if (!name || !categoryId) {
        toastError("Please fill all fields");
        return;
    }

    createSkillMutation.mutate(
      {
        name,
        category: categoryId,
      },
      {
        onSuccess: () => {
          toastSuccess("Skill created successfully");
          setName("");
          setCategoryId("");
          onClose();
        },
        onError: () => {
          toastError("Failed to create skill");
        }
      }
    );
  };

  const handleCancel = () => {
    setName("");
    setCategoryId("");
    onClose();
  };

  return (
    <Dialog
            open
            onClose={onClose}
      title="Add Skill"
      size="sm"
    >
        <div className="flex items-center justify-between px-8 pt-8 pb-6">
          <h2 className="text-[17px] font-semibold text-fg">Add Skill</h2>
          <button onClick={handleCancel} className="text-fg-subtle hover:text-fg-muted">✕</button>
        </div>

        <div className="px-8 pb-8">
          <p className="text-xs text-fg-muted mb-8">Add a new skill to your freelancing platform.</p>

          <form onSubmit={handleSubmit} className="space-y-6">
            <div className="space-y-2">
              <Field label="Skill Name">
                {(f) => (
                  <input
                    value={name}
                    onChange={(e) => setName(e.target.value)}
                    placeholder="e.g. React"
                    {...f}
                    className="w-full h-[50px] px-4 border rounded-lg focus-visible:ring-2 focus-visible:ring-brand outline-none"
                  />
                )}
              </Field>
            </div>

            <div className="space-y-2">
              <Field label="Category">
                {(f) => (
                  <select
                    disabled={isLoading}
                    value={categoryId}
                    onChange={(e) => setCategoryId(e.target.value)}
                    {...f}
                    className="w-full h-12 px-4 border rounded-lg focus-visible:ring-2 focus-visible:ring-brand outline-none appearance-none bg-surface"
                  >
                    {isLoading ? (
                      <option>Loading categories...</option>
                    ) : (
                      <>
                        <option value="">Select category</option>
                        {categories?.map((cat) => (
                          <option key={cat.id} value={cat.id}>{cat.name}</option>
                        ))}
                      </>
                    )}
                  </select>
                )}
              </Field>
            </div>

            <div className="flex gap-3 pt-2">
              <button
                type="button"
                onClick={handleCancel}
                className="flex-1 h-12 bg-surface-3 rounded-lg font-medium text-fg-muted hover:bg-surface-inset transition-colors"
              >
                Cancel
              </button>
              <Button
                type="submit"
                variant="primary"
                size="lg"
                loading={createSkillMutation.isPending}
                disabled={createSkillMutation.isPending || isLoading}
                className="flex-1"
              >
                Add Skill
              </Button>
            </div>
          </form>
        </div>
    </Dialog>
  );
}
