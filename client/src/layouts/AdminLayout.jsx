import { Outlet } from "react-router-dom";
import InfrastructureShell from "../components/InfrastructureShell.jsx";
import AdminSidebar from "../features/admin/dashboard/components/AdminSidebar.jsx";
import AdminFooter from "../features/admin/dashboard/components/AdminFooter.jsx";
import AdminHeader from "../features/admin/dashboard/components/AdminHeader.jsx";

export default function AdminLayout() {
  return <div className="admin-shell"><AdminSidebar /><main className="admin-shell__main"><AdminHeader /><Outlet /></main><AdminFooter /></div>;
}