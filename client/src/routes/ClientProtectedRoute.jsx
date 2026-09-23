import { Navigate, Outlet } from "react-router-dom";
import { useSelector } from "react-redux";
import { canAccessClientRoute } from "./clientRouteAccess.js";

export default function ClientProtectedRoute() {
  const { initialized, loading, user } = useSelector((state) => state.auth);

  if (!initialized || loading) {
    return null;
  }

  if (!canAccessClientRoute(user)) {
    return <Navigate to="/" replace />;
  }

  return <Outlet />;
}
