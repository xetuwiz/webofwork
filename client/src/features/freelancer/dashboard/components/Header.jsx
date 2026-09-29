import { Bell, Mail, Menu} from "lucide-react";
import { Link, useNavigate } from "react-router-dom";
import { useSwitchRole } from "../../../client/homepage/homePageMutation";
import { useSelector } from "react-redux";
import { useState, useEffect, useRef, useMemo } from "react";
import { useModal } from "../../../../hooks/modal/useModalStore";
import { useNotifications } from "../../../../components/notifications/notificationQueries";

import { useChatRooms } from "../../../../components/chat/chatQueries";

export function FreelancerHeader({onMenuClick}) {
  const { mutateAsync } = useSwitchRole();
  const user = useSelector((state) => state.auth.user);
  const navigate = useNavigate();
  const { openModal } = useModal();

  const [shouldAnimate, setShouldAnimate] = useState(false);
  const prevCountRef = useRef(0);
  const { data: rooms } = useChatRooms();
  const currentUserId = user?.id;

  const { data: notifications } = useNotifications(false);

  // No `useNotificationSocket` here on purpose.
  //
  // `RootLayout` already opens the notification socket for the whole
  // application, and `FreelancerLayout` renders this header *inside* RootLayout's
  // `<Outlet>`. Calling the hook in both places meant every freelancer page ran
  // two live sockets: two copies of every push, the notification sound played
  // twice, and the query cache written twice per message. Clients and admins were
  // unaffected because they have no header like this one, which is why it read as
  // working -- the duplication was specific to one role.
  //
  // RootLayout is the single owner because it is the only component mounted for
  // all three roles.

  // unread_count from the server rather than the length of the unread-only list:
  // the list is capped at `limit`, so a user with more unread than the cap would
  // see a badge lower than the real count.
  const unreadCount = notifications?.data?.unread_count || 0;

  useEffect(() => {
    if (unreadCount <= prevCountRef.current) {
      prevCountRef.current = unreadCount;
      return;
    }

    setShouldAnimate(true);
    const timer = setTimeout(() => {
      setShouldAnimate(false);
    }, 5000);
    prevCountRef.current = unreadCount;
    return () => clearTimeout(timer);
  }, [unreadCount]);

  const handleSwitchToClient = async () => {
    if (user?.is_freelancer) {
      await mutateAsync({ role: "client" });
      navigate("/");
    }
  };

    const unreadMessageCount = useMemo(() => {
      const roomList = rooms?.results || rooms || [];
      return roomList.reduce((acc, room) => {
        const lastMsg = room.last_message;
        if (lastMsg && !lastMsg.is_read && lastMsg.sender !== currentUserId) {
          return acc + 1;
        }
        return acc;
      }, 0);
    }, [rooms, currentUserId]);

  return (
    <header className="w-full h-[70px] md:h-[80px] bg-surface border-b border-line flex items-center justify-between px-4 md:px-10 sticky top-0 z-30">
      <div className="flex items-center gap-3">
        {/* Mobile Menu Toggle */}
        <button
          onClick={onMenuClick}
          className="lg:hidden p-2 hover:bg-surface-3 rounded-lg transition-colors"
          aria-label="Open navigation"
        >
          <Menu className="w-6 h-6 text-fg-muted" />
        </button>

        <Link to="/" className="flex items-center">
          <h1 className="text-[24px] md:text-[40px] leading-none font-[900] tracking-tight" style={{ fontFamily: 'MuseoModerno, sans-serif' }}>
            <span className="text-fg">Web</span>
            <span className="text-brand">OfWork</span>
          </h1>
        </Link>
      </div>

      <div className="flex items-center gap-2 md:gap-6">
        <button onClick={handleSwitchToClient} className="flex items-center gap-2 h-9 md:h-11 px-3 md:px-5 rounded-xl border border-brand-line bg-brand-soft hover:bg-brand-hover-soft/70 transition-colors">
          <svg width="16" height="16" viewBox="0 0 16 16" fill="none" aria-hidden="true">
             <path d="M5.33334 2L2.66667 4.66667L5.33334 7.33333" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/>
             <path d="M2.66667 4.6665H13.3333" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/>
             <path d="M10.6667 13.9998L13.3333 11.3332L10.6667 8.6665" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/>
             <path d="M13.3333 11.3335H2.66667" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/>
          </svg>
          <span className="hidden sm:inline text-xs md:text-sm font-bold text-brand-soft-fg">Switch to Client</span>
        </button>

        <div className="flex items-center gap-1 md:gap-2">
            <button onClick={() => openModal("messages")} className="relative w-9 h-9 md:w-11 md:h-11 rounded-full flex items-center justify-center hover:bg-surface-3 transition-colors" aria-label="Messages">
              <Mail className="w-5 h-5 md:w-6 md:h-6 text-fg-muted" />
              {unreadMessageCount > 0 && (
                <span className="absolute top-0 right-0 bg-brand rounded-full flex items-center justify-center px-1 min-w-[14px] h-3.5">
                  <span className="text-brand-fg text-[8px] font-semibold">{unreadMessageCount}</span>
                </span>
              )}
            </button>

            <button onClick={() => openModal("notifications")} className="relative w-9 h-9 md:w-11 md:h-11 flex items-center justify-center hover:bg-surface-3 rounded-full transition-colors" aria-label="Notifications">
              <Bell className={`w-5 h-5 md:w-6 md:h-6 text-fg-muted ${shouldAnimate ? 'animate-bounce text-brand' : ''}`} />
              {unreadCount > 0 && (
                <span className="absolute top-1 right-1 w-4 h-4 md:w-5 md:h-5 bg-brand border-2 border-surface rounded-full flex items-center justify-center text-[8px] md:text-[10px] font-bold text-brand-fg">
                  {unreadCount > 9 ? '9+' : unreadCount}
                </span>
              )}
            </button>
        </div>
      </div>
    </header>
  );
}
