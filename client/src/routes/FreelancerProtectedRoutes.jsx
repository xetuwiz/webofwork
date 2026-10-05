import { useSelector } from "react-redux";
import { useEffect, useRef } from "react";
import { Navigate } from "react-router-dom";
import NotFound from "../components/NotFound";
import { toastWarning } from "../utils/toast";

export default function FreelancerProtectedRoute({ children }) {
  const { user, initialized } = useSelector((state) => state.auth);

  // toast.warn used to be called during render, so React logged "Cannot update a
  // component while rendering a different component" on every navigation. Move it
  // into an effect and fire it at most once per mount using a ref.
  const warnedRef = useRef(false);
  useEffect(() => {
    if (!user && !warnedRef.current) {
      warnedRef.current = true;
      toastWarning("Login before entering to this page", {
        toastId: "login-required",
      });
    }
  }, [user]);

  if (!initialized) return null;

  if (!user) {
    return <Navigate to="/" replace />;
  }

  if (user.current_role !== "freelancer") {
    return <NotFound />;
  }

  return children;
}
