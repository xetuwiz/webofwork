import { Navigate, Outlet } from "react-router-dom";
import { useSelector } from "react-redux";

export default function AdminProtectedRoute() {
  const { initialized, loading, user } = useSelector((state) => state.auth);
  if (!initialized || loading) return null;
  if (!user) return <Navigate to="/admin/login" replace />;
  if (!user.is_admin && user.role !== "Admin") return <Navigate to="/" replace />;
  return <Outlet />;
}