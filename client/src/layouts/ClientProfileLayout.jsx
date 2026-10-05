import { useState } from "react";
import { Outlet } from "react-router-dom";
import { Menu, X } from "lucide-react";
import ProfileSidebar from "../features/client/profile/components/ProfileSidebar";

export default function ClientProfileLayout() {
  const [isSidebarOpen, setIsSidebarOpen] = useState(false);

  return (
    <div className="flex flex-col lg:flex-row lg:-mt-6 -mt-3 min-h-screen">
      <div className="lg:hidden flex items-center justify-between p-4 bg-surface border-b border-line sticky top-0 z-30">
        <h1 className="text-lg font-bold text-fg">Client Account</h1>
        <button
          onClick={() => setIsSidebarOpen(!isSidebarOpen)}
          className="p-2 text-fg-muted hover:bg-surface-3 rounded-lg transition-colors"
          aria-label={isSidebarOpen ? "Close navigation" : "Open navigation"}
        >
          {isSidebarOpen ? <X className="w-6 h-6" /> : <Menu className="w-6 h-6" />}
        </button>
      </div>

      {isSidebarOpen && (
        <div
          className="fixed inset-0 bg-black/50 z-40 lg:hidden"
          onClick={() => setIsSidebarOpen(false)}
        />
      )}

      <aside className={`
        fixed inset-y-0 lg:mt-5 mt-18 left-0 z-overlay w-72 bg-surface border-r border-line transform transition-transform duration-slow ease-emphasized lg:relative lg:translate-x-0 lg:z-auto lg:w-80 lg:sticky lg:top-0
        ${isSidebarOpen ? "translate-x-0" : "-translate-x-full"}
      `}>
        <ProfileSidebar onNavigate={() => setIsSidebarOpen(false)} />
      </aside>

      <main id="main-content" tabIndex={-1} className="flex-1 p-4 sm:p-6 bg-surface-2 overflow-x-hidden w-full">
        <Outlet />
      </main>
    </div>
  );
}
