import { Outlet } from "react-router-dom";
import InfrastructureShell from "../components/InfrastructureShell.jsx";
import AdminSidebar from "../features/admin/dashboard/components/AdminSidebar.jsx";
import AdminFooter from "../features/admin/dashboard/components/AdminFooter.jsx";

export default function AdminLayout() {
  return <div className="admin-shell"><AdminSidebar /><main className="admin-shell__main"><Outlet /></main><AdminFooter /></div>;
}