import { Outlet } from "react-router-dom";
import ClientHeader from "../features/client/homepage/components/ClientHeader";
import ClientFooter from "../features/client/homepage/components/ClientFooter";

export default function ClientLayout() {
  return (
    <div className="min-h-screen flex flex-col w-full overflow-x-hidden bg-surface-2">
      <ClientHeader />

      <main id="main-content" tabIndex={-1} className="flex-1 mt-19 bg-surface-2 w-full">
        <div className="w-full max-w-page-4xl mx-auto">
          <Outlet />
        </div>
      </main>

      <ClientFooter />
    </div>
  );
}
