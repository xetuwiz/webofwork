import { X, LogOut, Settings } from "lucide-react";
import { useSelector, useDispatch } from "react-redux";
import { Link, useLocation, useNavigate } from "react-router-dom";
import { logoutThunk } from "../../../client/account/auth/authThunks";
import { ThemeToggle } from "../../../../hooks/theme/ThemeToggle";
import { NAVIGATION } from "../../../../config/navigation";

// The freelancer destinations now live in the shared navigation config, so the
// command palette indexes the same list instead of a second copy of it.
const menuItems = NAVIGATION.filter((item) => item.group === "Freelancer");

export function Sidebar({ isOpen, onClose }) {
  const location = useLocation();
  const dispatch = useDispatch();
  const navigate = useNavigate();
  const { user } = useSelector(state => state.auth);

  // The freelancer panel had no logout control anywhere -- only the admin
  // sidebar and the client profile modal had one, so a freelancer had to
  // switch to the client role just to sign out. Mirrors AdminSidebar.
  const handleLogout = async () => {
    try {
      await dispatch(logoutThunk()).unwrap();
    } catch {
      // logoutThunk clears the auth slice even when the request fails.
    }
    onClose?.();
    navigate("/");
  };

  return (
    <aside className={`
      fixed inset-y-0 left-0 z-overlay w-64 bg-surface border-r border-line flex flex-col shrink-0 transition-transform duration-slow ease-emphasized lg:relative lg:translate-x-0
      ${isOpen ? "translate-x-0" : "-translate-x-full"}
    `}>
      <div className="h-[71px] md:h-[81px] border-b border-line flex items-center justify-between px-6">
        <h2 className="text-[18px] md:text-[20.4px] font-bold text-fg">
          Freelancer Panel
        </h2>
        <button onClick={onClose} className="lg:hidden p-1 text-fg-muted hover:bg-surface-3 rounded" aria-label="Close navigation">
          <X className="w-5 h-5" />
        </button>
      </div>

      <nav className="flex-1 px-4 py-4 overflow-y-auto scrollbar-themed">
        <div className="flex flex-col gap-1">
          {menuItems.map((item) => {
            const Icon = item.icon;
            const isActive = location.pathname === item.path;

            return (
              <Link
                key={item.path}
                to={item.path}
                onClick={onClose} // Close sidebar on link click (mobile)
                aria-current={isActive ? "page" : undefined}
                className={`group relative flex items-center gap-3 px-4 py-2.5 rounded-lg transition-colors ${
                  isActive
                    // Tinted rather than a solid brand block: the full-width solid
                    // fill made every row the same visual weight as the active one
                    // and, with `shadow-blue-200`, tinted the whole panel blue in
                    // light mode and near-black in dark.
                    ? "bg-brand-soft text-brand-soft-fg font-semibold"
                    : "text-fg-muted hover:bg-surface-3 hover:text-fg"
                }`}
              >
                {/* A 2px rail instead of a filled pill, so the active row reads as
                    "you are here" without a heavy block of colour. */}
                <span
                  aria-hidden="true"
                  className={`absolute left-0 top-1/2 h-5 w-0.5 -translate-y-1/2 rounded-full bg-brand transition-opacity ${
                    isActive ? "opacity-100" : "opacity-0"
                  }`}
                />
                <Icon className="w-[18px] h-[18px] shrink-0" strokeWidth={2} />
                <span className="text-sm">{item.label}</span>
              </Link>
            );
          })}
        </div>
      </nav>

      <div className="border-t border-line p-4 lg:mb-0">
        <Link to="/freelancer/profile" onClick={onClose} className="flex items-center gap-3 w-full hover:bg-surface-3 p-2 rounded-xl transition-colors">
          {user?.profile_photo ? (
            <img src={user?.profile_photo} alt="" className="w-10 h-10 rounded-full object-cover" />
          ) : (
            <span className="w-10 h-10 flex items-center justify-center rounded-full bg-brand text-brand-fg font-bold">
              {user?.full_name?.[0]}
            </span>
          )}
          <div className="flex flex-col min-w-0">
            <p className="text-sm font-semibold text-fg truncate">{user?.full_name}</p>
            <p className="text-[11px] text-fg-muted">View Profile</p>
          </div>
        </Link>

        {/* Theme control lives with the account block, not the page header: it is
            a persistent preference rather than a per-page action, and the header
            is already carrying the notification and chat triggers. */}
        <div className="mt-2 flex items-center gap-2">
          <ThemeToggle />
          <button
            onClick={handleLogout}
            className="flex flex-1 items-center gap-3 px-3 py-2.5 rounded-lg text-danger hover:bg-danger-soft transition-colors"
          >
            <LogOut className="w-[18px] h-[18px]" strokeWidth={2} />
            <span className="text-sm font-medium">Logout</span>
          </button>
        </div>
      </div>
    </aside>
  );
}
