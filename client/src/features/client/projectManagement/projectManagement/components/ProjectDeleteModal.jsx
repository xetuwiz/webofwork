import { Loader2, AlertTriangle } from "lucide-react";
import { useDeleteProject } from "../projectMutations";
import Dialog from "../../../../components/common/Dialog";
import Button from "../../../../components/common/Button";

export default function ProjectDeleteModal({ projectId, onClose }) {
  const { mutateAsync: deleteProject, isPending } = useDeleteProject();

  const handleDelete = async () => {
    try {
      await deleteProject({ id: projectId });
      onClose();
    } catch (error) {
      console.error("Deletion failed:", error);
    }
  };

  return (
    <Dialog
      open
      onClose={onClose}
      title="Delete Project"
      size="md"
    >
        <div className="p-6">
          <div className="flex items-center gap-3 mb-4">
            <div className="p-2 bg-danger-soft rounded-full">
              <AlertTriangle className="w-6 h-6 text-danger" />
            </div>
            <h2 className="text-xl font-bold text-fg">
              Delete Project
            </h2>
          </div>

          <p className="text-fg-muted leading-relaxed">
            Are you sure you want to delete this project? This action is permanent and <span className="font-semibold text-danger">cannot be undone</span>.
          </p>

          <div className="mt-8 flex flex-col-reverse sm:flex-row justify-end gap-3">
            <button
              onClick={onClose}
              disabled={isPending}
              className="w-full sm:w-auto px-6 py-2.5 rounded-xl border border-line text-sm font-semibold text-fg-muted hover:bg-surface-2 transition-colors disabled:opacity-50"
            >
              Cancel
            </button>

            <Button
              onClick={handleDelete}
              variant="danger"
              size="none"
              loading={isPending}
              className="w-full sm:w-auto px-6 py-2.5 rounded-xl text-sm active:scale-95 disabled:opacity-70 shadow-lg shadow-red-200"
            >
              {isPending ? "Deleting..." : "Delete Project"}
            </Button>
          </div>
        </div>
    </Dialog>
  );
}
