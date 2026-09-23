import { BrowserRouter, Navigate, Route, Routes } from "react-router-dom";
import AdminLayout from "../layouts/AdminLayout.jsx";
import RootLayout from "../layouts/RootLayout.jsx";
import NotFound from "../components/NotFound.jsx";
import RoutePlaceholder from "../components/RoutePlaceholder.jsx";
import AdminProtectedRoute from "./AdminProtectedRoutes.jsx";
import AdminRoutes from "./AdminRoutes.jsx";
import ClientRoutes from "./ClientRoutes.jsx";
import FreelancerProtectedRoute from "./FreelancerProtectedRoutes.jsx";
import FreelancerRoutes from "./FreelancerRoutes.jsx";
import AdminLogin from "../features/admin/auth/AdminLogin.jsx";
import ResetPasswordPage from "../features/client/account/pages/ResetPasswordPage.jsx";

export default function AppRoutes() {
  return (
    <BrowserRouter>
      <Routes>
        <Route element={<RootLayout />}>
          <Route path="/admin/login" element={<AdminLogin />} />
          <Route path="/reset-password" element={<ResetPasswordPage />} />
          <Route element={<AdminProtectedRoute />}>
            <Route path="/admin" element={<AdminLayout />}>
              <Route path="*" element={<AdminRoutes />} />
            </Route>
          </Route>
          <Route element={<FreelancerProtectedRoute />}>
            <Route path="/freelancer/*" element={<FreelancerRoutes />} />
          </Route>
          <Route path="/*" element={<ClientRoutes />} />
          <Route path="/404" element={<NotFound />} />
          <Route path="*" element={<Navigate to="/404" replace />} />
        </Route>
      </Routes>
    </BrowserRouter>
  );
}