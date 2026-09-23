import { Navigate, Outlet } from "react-router-dom";
import { useSelector } from "react-redux";

export default function AdminProtectedRoute() {
  const { initialized, loading, user } = useSelector((state) => state.auth);
  if (!initialized || loading) return null;
  if (!user) return <Navigate to="/admin/login" replace />;
  const role = String(user.role ?? user.Role ?? "").toLowerCase();
  if (!user.is_admin && role !== "admin" && role !== "3") return <Navigate to="/" replace />;
  return <Outlet />;
}