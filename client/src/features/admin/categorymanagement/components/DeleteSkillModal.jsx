import React from 'react';
import { useDeleteSkill } from "../skillMutations";
import Dialog from "../../../../components/common/Dialog";
import { toastSuccess } from "../../../../utils/toast";
import { getErrorMessage } from "../../../../utils/errorMessage";

export default function DeleteSkillModal({ open, onOpenChange, skill }) {
  const deleteSkillMutation = useDeleteSkill();

  if (!open || !skill) return null;

  const handleDelete = () => {
    deleteSkillMutation.mutate(skill.id, {
      onSuccess: () => {
        onOpenChange(false);
      },
    });
    toastSuccess("Skill removed")
  };

  const handleCancel = () => {
    deleteSkillMutation.reset();

    onOpenChange(false);
  };

  // Covers the array-of-strings, ProblemDetails `detail`, and plain `error` shapes
  // in one call. Deleting a skill that projects still reference fails with a
  // specific reason from the API, and that is what the admin needs to see.
  const errorMessage = getErrorMessage(
    deleteSkillMutation.error,
    "An error occurred while deleting."
  );

  return (
    <Dialog
            open={open}
            onClose={() => onOpenChange(false)}
      title="Delete Skill"
      size="sm"
      className="overflow-hidden"
    >
        
        <div className="flex items-center justify-between px-8 pt-8 pb-6">
          <h2 className="text-[17px] font-semibold text-fg">Delete Skill</h2>
          <button onClick={handleCancel} className="text-fg-subtle hover:text-fg-muted">✕</button>
        </div>

        <div className="px-8 pb-8">
          {deleteSkillMutation.isError && (
            <div className="mb-4 p-3 bg-danger-soft border border-danger/30 rounded-lg">
              <p className="text-xs text-danger font-medium text-center">
                {errorMessage}
              </p>
            </div>
          )}

          <p className="text-xs text-fg-muted mb-4">
            Are you sure you want to delete this skill?
          </p>

          <div className="bg-surface-2 rounded-lg px-4 py-4 mb-6">
            <p className="text-sm font-medium text-fg">{skill.name}</p>
          </div>

          <p className="text-xs text-fg-subtle mb-8">
            This action cannot be undone.
          </p>

          <div className="flex gap-3">
            <button
              type="button"
              onClick={handleCancel}
              className="flex-1 h-12 bg-surface-3 rounded-lg font-medium text-sm"
            >
              Cancel
            </button>
            <button
              type="button"
              onClick={handleDelete}
              disabled={deleteSkillMutation.isPending}
              className="flex-1 h-12 bg-danger-soft text-danger-fg rounded-lg hover:bg-danger disabled:opacity-50 font-medium text-sm"
            >
              {deleteSkillMutation.isPending ? "Deleting..." : "Delete Skill"}
            </button>
          </div>
        </div>
    </Dialog>
  );
}
