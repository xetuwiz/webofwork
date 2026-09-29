import { X, AlertTriangle } from "lucide-react";
import Dialog from "../../../../components/common/Dialog";

export default function SuspendUserModal({ isOpen, onClose, modalProps }) {
  if (!isOpen) return null;

  const { userName, userEmail, onConfirm, isActive } = modalProps;

  const handleAction = () => {
    onConfirm();
    onClose();
  };

  return (
    <Dialog
      open={isOpen}
      onClose={onClose}
      title={isActive ? "Suspend User" : "Activate User"}
      size="sm"
      className="overflow-hidden flex flex-col"
    >
        <div className="px-6 py-6 border-b border-line-soft">
          <div className="flex items-start justify-between">
            <div>
              <h2 className="text-[17px] font-semibold text-fg leading-7 mb-1">
                {isActive ? "Suspend User" : "Activate User"}
              </h2>
              <p className="text-[12px] text-fg-muted leading-5">
                {isActive 
                  ? "Are you sure you want to suspend this user? They will not be able to log in until reactivated."
                  : "Are you sure you want to reactivate this user? They will regain full access immediately."}
              </p>
            </div>
            <button
              onClick={onClose}
              className="p-1 -mt-1 -mr-1 hover:bg-surface-3 rounded-full transition-colors flex-shrink-0"
            >
              <X className="w-5 h-5 text-fg-subtle" strokeWidth={2} />
            </button>
          </div>
        </div>

        <div className="px-6 py-5 flex flex-col gap-4">
          <div className="bg-surface-2 border border-line rounded-xl p-[17px] flex items-center gap-3">
            <div className="w-12 h-12 rounded-full bg-brand flex items-center justify-center flex-shrink-0 shadow-inner">
              <span className="text-[14px] font-bold text-white uppercase">
                {userName?.charAt(0) || "U"}
              </span>
            </div>
            <div className="flex flex-col min-w-0">
              <p className="text-[14px] font-medium text-fg leading-6 truncate">
                {userName}
              </p>
              <p className="text-[12px] text-fg-muted leading-5 truncate">
                {userEmail}
              </p>
            </div>
          </div>

          <div className={`rounded-xl p-[17px] flex gap-3 border ${isActive ? 'bg-danger-soft border-danger/30' : 'bg-success-soft border-success/30'}`}>
            <AlertTriangle className={`w-5 h-5 flex-shrink-0 mt-0.5 ${isActive ? 'text-danger' : 'text-success'}`} />
            <div className="flex-1 min-w-0">
              <p className={`text-[12px] leading-5 font-medium ${isActive ? 'text-danger-soft-fg' : 'text-success-soft-fg'}`}>
                {isActive 
                  ? "This action can be reversed anytime using the 'Activate User' option." 
                  : "The user will be notified that their account access has been restored."}
              </p>
            </div>
          </div>
        </div>

        <div className="px-6 py-[17px] border-t border-line-soft bg-surface-2 flex items-center justify-end gap-3">
          <button
            onClick={onClose}
            className="px-4 py-[9px] text-[12px] font-medium text-fg-muted hover:bg-surface-inset rounded-lg transition-colors"
          >
            Cancel
          </button>
          <button
            onClick={handleAction}
            className={`px-5 py-[9px] rounded-lg text-[12px] font-medium text-white transition-all shadow-sm active:scale-95 ${
              isActive ? 'bg-danger hover:bg-danger' : 'bg-success hover:bg-success'
            }`}
          >
            {isActive ? "Suspend User" : "Activate User"}
          </button>
        </div>
    </Dialog>
  );
}
