import { useEffect, useState, useRef, useMemo } from "react";
import { Popover, PopoverTrigger, PopoverContent, PopoverAnchor } from '../../../../components/common/Popover';
import { Link, useLocation } from "react-router-dom";
import { Plus, Bell, Mail, Menu, X } from "lucide-react";
import { useSelector } from "react-redux";
import { useModal } from "../../../../hooks/modal/useModalStore";
import { useNotifications } from "../../../../components/notifications/notificationQueries";
import { useChatRooms } from "../../../../components/chat/chatQueries";
import Header from "../../landingPage/components/Header";
import { FreelancerHeader } from "../../../freelancer/dashboard/components/Header";
import AdminHeader from "../../../admin/dashboard/components/AdminHeader";
import { ThemeToggle } from "../../../../hooks/theme/ThemeToggle";

export default function ClientHeader() {
  const [isMenuOpen, setIsMenuOpen] = useState(false);
  const [shouldAnimate, setShouldAnimate] = useState(false);
  const prevCountRef = useRef(0);
  const location = useLocation();

  const user = useSelector((state) => state.auth.user);
  const currentUserId = user?.id;

  const { data: notifications } = useNotifications(false, { enabled: !!currentUserId });
  const { data: rooms } = useChatRooms({ enabled: !!currentUserId });

  useEffect(() => {
    setIsMenuOpen(false);
  }, [location.pathname]);

  // Server-provided unread_count, not the length of the unread-only list: the
  // list is capped at `limit`, so a user above the cap saw an undercount badge.
  const unreadNotifications = notifications?.data?.unread_count || 0;

  const unreadMessageCount = useMemo(() => {
    const roomList = Array.isArray(rooms?.results)
      ? rooms.results
      : Array.isArray(rooms)
        ? rooms
        : [];
    if (roomList.length === 0) return 0;
    return roomList?.reduce((acc, room) => {
      const lastMsg = room.last_message;
      if (lastMsg && !lastMsg.is_read && lastMsg.sender !== currentUserId) {
        return acc + 1;
      }
      return acc;
    }, 0);
  }, [rooms, currentUserId]);

  const { openModal } = useModal();

  useEffect(() => {
    if (unreadNotifications > prevCountRef.current) {
      setShouldAnimate(true);
      const timer = setTimeout(() => setShouldAnimate(false), 1000);
      return () => clearTimeout(timer);
    }
    prevCountRef.current = unreadNotifications;
  }, [unreadNotifications]);

  // The three early returns below were ABOVE these hooks, so logging out changed
  // how many hooks this component called. React tracks hooks by call order, so
  // the surviving hooks then lined up against the wrong state slots -- state
  // corruption, not a console warning. All hooks now run unconditionally.
  if (!user) return <Header />;
  if (user?.current_role === 'freelancer') return <FreelancerHeader />;
  if (user?.is_admin) return <AdminHeader />;

  const fullName = user?.full_name ?? "";
  const displayName = fullName.split(" ")[0];
  const profilePic = user?.profile_photo ?? null;

  const isActive = (path) => location.pathname === path;

  // One definition for all four nav links. The previous version repeated
  // `isActive(x) ? "text-brand" : "text-fg"` inline with a mix of
  // text-base / text-sm / text-lg per link, so the four items rendered at three
  // different sizes and the "active" treatment was the only thing they shared.
  const navLinkClass = (path) =>
    `text-sm xl:text-base font-semibold transition-colors ${
      isActive(path) ? "text-brand" : "text-fg hover:text-brand"
    }`;

  return (
    <header className="fixed top-0 z-raised w-full border-b border-line bg-surface/95 backdrop-blur-sm">
      {/* The mobile menu was a `useState` boolean plus a conditional
          absolutely-positioned div: no Escape, no outside click, no focus
          return. <Popover> supplies all three. It is anchored to the header row
          via `PopoverAnchor` rather than to the trigger, because the panel is
          full-width -- anchoring it to the hamburger would mis-position it. */}
      <Popover open={isMenuOpen} onOpenChange={setIsMenuOpen}>
        <PopoverAnchor asChild>
          <div className="w-full xl:px-8 px-4 flex h-[73px] items-center justify-between">
            <div className="flex items-center gap-2">
              <PopoverTrigger
                className="xl:hidden p-2 hover:bg-surface-3 rounded-md transition-colors"
                aria-label={isMenuOpen ? "Close menu" : "Open menu"}
              >
                {isMenuOpen ? <X className="h-6 w-6 text-fg" /> : <Menu className="h-6 w-6 text-fg" />}
              </PopoverTrigger>
          <Link to={"/"} className="flex items-center gap-2 md:gap-4 shrink-0">
            <h1 className="text-[22px] md:text-[32px] xl:text-[37px] font-bold" style={{ fontFamily: "MuseoModerno, sans-serif" }}>
              <span className="text-fg">Web</span>
              <span className="text-brand">OfWork</span>
            </h1>
          </Link>
        </div>

        <nav className="hidden xl:flex items-center gap-6 xxl:gap-9">
          <Link to="/" className={navLinkClass("/")}>Dashboard</Link>
          <Link to={'/find-talents'} className={navLinkClass("/find-talents")}>Find Talent</Link>
          <Link to={'/how-it-works'} className={navLinkClass("/how-it-works")}>How It Works</Link>
          <Link to="/my-projects" className={navLinkClass("/my-projects")}>My Posts</Link>
        </nav>

        <div className="flex items-center gap-2 md:gap-4 xl:gap-8">
          <Link
            to="/create-project"
            className={`hidden sm:flex h-10 xl:h-12 items-center gap-2.5 rounded-lg border-2 px-4 xl:px-6 transition-colors ${
              isActive("/create-project")
                ? "border-brand bg-brand-soft text-brand-soft-fg"
                // `border-black` had to become a token: an outlined button with a
                // hard black border is the one element that stays legible in
                // light mode and disappears completely in dark mode.
                : "border-line-strong bg-transparent text-fg hover:bg-surface-3"
            }`}
          >
            <Plus className="h-5 w-5 xl:h-7 xl:w-7" />
            <span className="text-sm xl:text-base font-bold whitespace-nowrap">Post a Project</span>
          </Link>

          {/* Present on all three authenticated headers, so the theme is reachable
              without hunting through a settings page. */}
          <ThemeToggle />

          <div className="flex items-center gap-1 md:gap-3 xl:gap-6">
            <button className="relative p-1.5 hover:bg-surface-3 rounded-full transition-colors" onClick={() => openModal("notifications")} aria-label="Notifications">
              <Bell className={`h-5 w-5 md:h-6 md:w-6 text-fg-subtle ${shouldAnimate ? 'animate-bounce text-brand' : ''}`} />
              {unreadNotifications > 0 && (
                <span className="absolute right-0.5 top-0.5 flex h-4 w-4 items-center justify-center rounded-full bg-brand text-[9px] font-bold text-brand-fg">
                  {unreadNotifications > 9 ? '9+' : unreadNotifications}
                </span>
              )}
            </button>

            <button className="relative p-1.5 hover:bg-surface-3 rounded-lg transition-colors" onClick={() => openModal("messages")} aria-label="Messages">
              <Mail className="h-5 w-5 md:h-6 text-fg-muted" />
              {unreadMessageCount > 0 && (
                <span className="absolute top-0.5 right-0.5 min-w-[16px] h-4 bg-brand rounded-full flex items-center justify-center px-1">
                  <span className="text-brand-fg text-[9px] font-semibold">{unreadMessageCount}</span>
                </span>
              )}
            </button>
          </div>

          <button data-profile-button='true' onClick={() => openModal("profile-menu")} className="flex items-center gap-2 md:gap-3 shrink-0">
            <div className="hidden md:block text-right">
              <div className="text-sm font-semibold text-fg">{displayName}</div>
              <div className="text-[10px] text-brand uppercase font-bold">Client</div>
            </div>
            <div className="h-9 w-9 md:h-11 md:w-11 rounded-full overflow-hidden border border-line shrink-0">
              {profilePic ? (
                <img src={profilePic} alt="" className="h-full w-full object-cover" />
              ) : (
                <div className="bg-brand h-full w-full flex items-center justify-center">
                  <span className="text-brand-fg font-bold text-lg">{fullName?.charAt(0).toUpperCase()}</span>
                </div>
              )}
            </div>
          </button>
        </div>
          </div>
        </PopoverAnchor>

        <PopoverContent
          aria-label="Main navigation"
          side="bottom"
          align="start"
          sideOffset={0}
          className="xl:hidden w-[--radix-popover-trigger-width] rounded-none border-x-0 border-b-0 border-t-0 bg-surface p-0 shadow-lg animate-in slide-in-from-top duration-slow"
        >
          <div className="py-4 px-6 flex flex-col gap-4">
            <Link to="/" className={navLinkClass("/")} onClick={() => setIsMenuOpen(false)}>Dashboard</Link>
            <Link to={'/find-talents'} className={navLinkClass("/find-talents")} onClick={() => setIsMenuOpen(false)}>Find Talent</Link>
            <Link to={'/how-it-works'} className={navLinkClass("/how-it-works")} onClick={() => setIsMenuOpen(false)}>How It Works</Link>
            <Link to="/my-projects" className={navLinkClass("/my-projects")} onClick={() => setIsMenuOpen(false)}>My Projects</Link>
            <Link to="/create-project" className="sm:hidden flex items-center gap-2 text-brand font-bold" onClick={() => setIsMenuOpen(false)}><Plus size={18}/> Post a Project</Link>
          </div>
        </PopoverContent>
      </Popover>
    </header>
  );
}
