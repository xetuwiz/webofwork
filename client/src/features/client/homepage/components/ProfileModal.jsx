import { useEffect, useRef } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useDispatch, useSelector } from "react-redux"; 
import { logoutThunk } from "../../account/auth/authThunks";
import { useSwitchRole } from "../homePageMutation";

export default function ProfileModal({ isOpen, onClose }) {
  const menuRef = useRef(null);
  const dispatch = useDispatch();
  const navigate = useNavigate();
  const { mutateAsync } = useSwitchRole();
  const user = useSelector((state) => state.auth.user);

  useEffect(() => {
    function handleClickOutside(event) {
      const toggleButton = document.querySelector('[data-profile-button="true"]');
      if (
        menuRef.current &&
        !menuRef.current.contains(event.target) &&
        (!toggleButton || !toggleButton.contains(event.target))
      ) {
        onClose();
      }
    }

    // Escape closes the menu. Without it a keyboard user could open this menu
    // and could not dismiss it with the keyboard -- the only way out was to
    // guess where to click. This is the same defect the six page-level menus
    // had, and the one reason this menu is still hand-rolled rather than a
    // <Popover>: it is opened through `openModal("profile-menu")` from three
    // different headers, so the trigger and the panel live in separate trees.
    // Migrating it means moving the Root up into each header; until then this
    // closes the gap without touching the logout path.
    function handleKeyDown(event) {
      if (event.key === "Escape") onClose();
    }

    if (isOpen) {
      document.addEventListener("mousedown", handleClickOutside);
      document.addEventListener("touchstart", handleClickOutside);
      document.addEventListener("keydown", handleKeyDown);
    }

    return () => {
      document.removeEventListener("mousedown", handleClickOutside);
      document.removeEventListener("touchstart", handleClickOutside);
      document.removeEventListener("keydown", handleKeyDown);
    };
  }, [isOpen, onClose]);

  if (!isOpen) return null;

  const handleLogout = () => {
    dispatch(logoutThunk());
    onClose();
    navigate("/");
  };

  const handleSwitchToFreelancer = async() => {
    onClose(); 
    if (user?.is_freelancer) {
      await mutateAsync({ role: "freelancer" });
      navigate("/freelancer");
    } else {
      navigate('/onBoarding')
    }
  };

  return (
    <div
      ref={menuRef}
      className="fixed right-4 top-[74px] z-popover w-[calc(100%-32px)] sm:w-80 rounded-2xl border border-line-soft bg-surface p-4 shadow-2xl animate-in fade-in zoom-in-95 duration-base"
    >
      <div className="flex flex-col gap-2.5">
        <Link
          to="/profile"
          onClick={onClose}
          className="flex items-center px-4 py-3 rounded-xl bg-surface-3 hover:bg-surface-inset transition-colors"
        >
          <span className="text-sm font-medium text-fg-muted">Profile</span>
        </Link>

        <Link
          to="/contracts"
          onClick={onClose}
          className="flex items-center px-4 py-3 rounded-xl bg-surface-3 hover:bg-surface-inset transition-colors"
        >
          <span className="text-sm font-medium text-fg-muted">My Contracts</span>
        </Link>

        <Link
          to="/profile/settings"
          onClick={onClose}
          className="flex items-center px-4 py-3 rounded-xl bg-surface-3 hover:bg-surface-inset transition-colors"
        >
          <span className="text-sm font-medium text-fg-muted">Settings</span>
        </Link>

        <Link
          to="/support"
          onClick={onClose}
          className="flex items-center px-4 py-3 rounded-xl bg-surface-3 hover:bg-surface-inset transition-colors"
        >
          <span className="text-sm font-medium text-fg-muted">Contact Support</span>
        </Link>
      </div>

      <div className="mt-4 pt-4 border-t border-line-soft flex flex-col gap-2">
        <button 
          onClick={handleSwitchToFreelancer}
          className="w-full bg-brand-soft text-brand-fg py-3 rounded-lg font-medium text-sm hover:bg-brand-hover transition-colors"
        >
          Switch to Freelancer
        </button>
        <button
          onClick={handleLogout}
          className="w-full py-3 text-danger font-medium text-sm hover:bg-danger-soft rounded-lg transition-colors"
        >
          Logout
        </button>
      </div>
    </div>
  );
}
