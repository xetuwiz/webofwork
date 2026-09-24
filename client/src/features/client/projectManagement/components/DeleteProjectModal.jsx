export default function DeleteProjectModal({ project, onClose, onConfirm, pending, isOpen = false }) {
  if (!isOpen || !project) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 backdrop-blur-sm p-4">
      <div className="w-full max-w-md rounded-2xl bg-white p-6 shadow-xl">
        <h3 className="text-xl font-bold text-slate-900">Archive project</h3>
        <p className="mt-3 text-sm text-slate-600">
          This will remove <span className="font-semibold text-slate-900">{project.title}</span> from your active project list. This action cannot be undone.
        </p>
        <div className="mt-6 flex justify-end gap-3">
          <button type="button" onClick={onClose} className="rounded-xl border border-slate-200 px-4 py-2.5 text-sm font-medium text-slate-700 hover:bg-slate-50">Cancel</button>
          <button type="button" onClick={onConfirm} disabled={pending} className="rounded-xl bg-red-600 px-4 py-2.5 text-sm font-semibold text-white hover:bg-red-700 disabled:cursor-not-allowed disabled:opacity-60">
            {pending ? "Archiving..." : "Archive"}
          </button>
        </div>
      </div>
    </div>
  );
}
