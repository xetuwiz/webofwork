import { Link, useLocation, useNavigate } from "react-router-dom";
import { LogOut } from "lucide-react";
import { logoutThunk } from "../../../client/account/auth/authThunks";
import { useDispatch } from "react-redux";
import { ThemeToggle } from "../../../../hooks/theme/ThemeToggle";
import { NAVIGATION } from "../../../../config/navigation";

// Shared with the command palette -- see src/config/navigation.js.
const navigationItems = NAVIGATION.filter((item) => item.group === "Admin");

export default function AdminSidebar() {
  const dispatch = useDispatch()
  const location = useLocation();
  const navigate = useNavigate()
const handleLogout = async () => {
  try {
    await dispatch(logoutThunk()).unwrap();
    navigate('/', { replace: true });
  } catch (error) {
    navigate('/', { replace: true });
  }
};
  return (
    <div className="w-64 h-screen bg-surface border-r border-line flex flex-col fixed left-0 top-0 z-overlay">

      {/* The wordmark used to be `text-[37px] ... leading-7` inside `px-9`.
          That is three separate bugs stacked:
            - `px-9` is 2.25rem per side, leaving 256 - 72 = 184px of a 256px
              sidebar, while "WebOfWork" at 37px extrabold needs ~250px, so it
              overflowed horizontally;
            - `leading-7` is a 28px line box for a 37px glyph, so ascenders and
              descenders were clipped vertically;
            - the box was `h-20` with no `border-b`, unlike every other sidebar
              header in the app, so it also just looked different.
          Now the size is in viewport-relative units with a sane floor, the
          padding matches the `px-4` used by the nav below it, and the header
          lines up with the rest of the app. */}
      <div className="h-20 shrink-0 flex items-center px-4 border-b border-line">
        <h1 className="text-[clamp(1.125rem,4.2vw,1.75rem)] font-extrabold leading-tight font-museo whitespace-nowrap">
          <span className="text-fg">Web</span>
          <span className="text-brand">OfWork</span>
        </h1>
      </div>

      <nav className="flex-1 px-4 pt-5 overflow-y-auto scrollbar-themed">
        <p className="text-[10.2px] font-semibold text-fg-subtle px-2 mb-4">
          Admin Navigation
        </p>

        <div className="space-y-1">
          {navigationItems.map((item) => {
            const Icon = item.icon;
            const isActive = location.pathname === item.path;

            return (
              <Link
                key={item.label}
                to={item.path}
                aria-current={isActive ? "page" : undefined}
                className={`group relative flex items-center gap-3 px-3 py-2.5 rounded-lg transition-colors ${
                  // Identical to the freelancer sidebar: same tint, same 2px active
                  // rail. These two menus were the most visible inconsistency --
                  // one used a tinted pill, the other a solid brand block.
                  isActive
                    ? "bg-brand-soft text-brand-soft-fg font-semibold"
                    : "text-fg-muted hover:bg-surface-3 hover:text-fg"
                }`}
              >
                <span
                  aria-hidden="true"
                  className={`absolute left-0 top-1/2 h-5 w-0.5 -translate-y-1/2 rounded-full bg-brand transition-opacity ${
                    isActive ? "opacity-100" : "opacity-0"
                  }`}
                />
                <Icon className="w-[18px] h-[18px] shrink-0" strokeWidth={2} />
                <span className="text-xs">{item.label}</span>
              </Link>
            );
          })}
        </div>
      </nav>

      <div className="border-t border-line p-4">
        <div className="flex items-center gap-2">
          <ThemeToggle />
          <button onClick={handleLogout} className="flex flex-1 items-center gap-3 px-3 py-2.5 rounded-lg text-danger hover:bg-danger-soft transition-colors text-left">
            <LogOut className="w-[18px] h-[18px] shrink-0" strokeWidth={2} />
            <span className="text-xs font-medium">Sign Out</span>
          </button>
        </div>
      </div>
    </div>
  );
}
