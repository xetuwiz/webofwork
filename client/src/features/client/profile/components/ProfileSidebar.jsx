import { NavLink } from "react-router-dom";
import { Trash2 } from "lucide-react";
import { useModal } from "../../../../hooks/modal/useModalStore";
import { NAVIGATION } from "../../../../config/navigation";


// Shared with the command palette -- see src/config/navigation.js.
const navigation = NAVIGATION.filter((item) => item.group === "Profile");

export default function ProfileSidebar({ onNavigate }) {
  const { openModal } = useModal()
  return (
    <div className="h-full  md:mt-8 px-6 pt-6 overflow-y-auto">
      <div className="mb-8">
        <h1 className="text-2xl font-bold text-fg mb-1">
          Client Account
        </h1>
        <p className="text-sm text-fg-subtle">
          Manage your profile and settings
        </p>
      </div>

      <nav className="flex-1 space-y-1">
        {navigation.map((item) => {
          return (
            <NavLink
              key={item.label}
              to={item.path}
              end
              onClick={onNavigate}
              className={({ isActive }) =>
              `flex items-center gap-3 px-4 py-3 rounded-lg text-sm font-medium transition-colors ${
                isActive
                  ? "bg-brand-soft text-white shadow-sm"
                  : "text-fg-muted hover:bg-surface-2"
              }`
            }
          >
              {({ isActive }) => (
              <>
                <item.icon
                  className={`w-5 h-5 ${isActive ? "text-white" : "text-fg-subtle"}`}
                  strokeWidth={2}
                />
                <span>{item.label}</span>
              </>
            )}
            </NavLink>
          );
        })}

        <div className="pt-4">
          <button
            onClick={() => {
              openModal("delete-account");
              if(onNavigate) onNavigate(); 
            }}
            className="w-full flex items-center gap-3 px-4 py-3 rounded-lg text-sm font-medium text-danger hover:bg-danger-soft transition-colors"
          >
            <Trash2 className="w-5 h-5 text-danger" strokeWidth={2} />
            <span>Delete Account</span>
          </button>

        </div>
      </nav>
      </div>
  );
}