import { Outlet } from "react-router-dom";
import ProfileSidebar from "../features/client/profile/components/ProfileSidebar.jsx";

export default function ClientProfileLayout() {
  return (
    <div className="min-h-screen bg-slate-50">
      <div className="mx-auto max-w-7xl px-4 py-6 lg:px-6">
        <div className="flex flex-col gap-6 md:flex-row">
          <aside className="md:w-72">
            <div className="rounded-2xl border border-slate-200 bg-white shadow-sm">
              <ProfileSidebar onNavigate={() => {}} />
            </div>
          </aside>
          <main className="flex-1">
            <Outlet />
          </main>
        </div>
      </div>
    </div>
  );
}