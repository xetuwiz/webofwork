import { useEffect, useState } from "react";
import { useUpdateSkill } from "../skillMutations";
import Dialog from "../../../../components/common/Dialog";
import { toastSuccess } from "../../../../utils/toast";
import { Field } from "../../../../components/common/Field";

export default function EditSkillModal({ open, onOpenChange, skill, categories }) {
  const [name, setName] = useState("");
  const [categoryId, setCategoryId] = useState("");
  console.log(categories.data);
  

  const updateSkillMutation = useUpdateSkill();

  useEffect(() => {
    if (open && skill) {
      setName(skill.name);
      setCategoryId(skill.category);
    }
  }, [open, skill]);

  if (!open || !skill) return null;

  const handleSubmit = (e) => {
    e.preventDefault();

    updateSkillMutation.mutate(
      {
        id: skill.id,
        data: {
          name,
          category: categoryId,
        },
      },
      {
        onSuccess: () => {
          onOpenChange(false);
        },
      }
    );
    toastSuccess("skill updated")
  };

  const handleCancel = () => {
    onOpenChange(false);
  };

  return (
    <Dialog
            open={open}
            onClose={() => onOpenChange(false)}
      title="Edit Skill"
      size="sm"
    >
        
        <div className="flex items-center justify-between px-8 pt-8 pb-6">
          <h2 className="text-[17px] font-semibold text-fg">
            Edit Skill
          </h2>
          <button
            onClick={handleCancel}
            className="text-fg-subtle hover:text-fg-muted"
          >
            ✕
          </button>
        </div>

        <div className="px-8 pb-8">
          <p className="text-xs text-fg-muted mb-8">
            Update the skill details below.
          </p>

          <form onSubmit={handleSubmit} className="space-y-6">
            
            <div className="space-y-2">
              <Field label="Skill Name">
                {(f) => (
                  <input
                    value={name}
                    onChange={(e) => setName(e.target.value)}
                    {...f}
                    className="w-full h-[50px] px-4 border rounded-lg focus-visible:ring-2 focus-visible:ring-brand"
                  />
                )}
              </Field>
            </div>

            <div className="space-y-2">
              <Field label="Category">
                {(f) => (
                  <select
                    value={categoryId}
                    onChange={(e) => setCategoryId(e.target.value)}
                    {...f}
                    className="w-full h-12 px-4 border rounded-lg focus-visible:ring-2 focus-visible:ring-brand"
                  >
                    <option value="">Select category</option>
                    {categories?.data.map((cat) => (
                      <option key={cat.id} value={cat.id}>
                        {cat.name}
                      </option>
                    ))}
                  </select>
                )}
              </Field>
            </div>

            <div className="flex gap-3 pt-2">
              <button
                type="button"
                onClick={handleCancel}
                className="flex-1 h-12 bg-surface-3 rounded-lg"
              >
                Cancel
              </button>
              <button
                type="submit"
                disabled={updateSkillMutation.isPending}
                className="flex-1 h-12 bg-brand text-brand-fg rounded-lg"
              >
                Save Changes
              </button>
            </div>

          </form>
        </div>
    </Dialog>
  );
}
