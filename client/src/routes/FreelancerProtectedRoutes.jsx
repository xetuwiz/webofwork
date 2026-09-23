import { Navigate, Outlet } from "react-router-dom";
import { useSelector } from "react-redux";

export default function FreelancerProtectedRoute() {
  const { initialized, loading, user } = useSelector((state) => state.auth);
  if (!initialized || loading) return null;
  if (!user) return <Navigate to="/" replace />;
  if (user.current_role && user.current_role !== "freelancer") return <Navigate to="/" replace />;
  return <Outlet />;
}