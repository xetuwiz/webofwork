import { lazy, Suspense } from 'react';
import RouteFallback from '../components/common/RouteFallback';
import { ErrorBoundary } from '../components/common/ErrorBoundary';
import RootLayout from '../layouts/RootLayout'
import ProtectedRoute from './ClientProtectedRoute'
import AdminProtectedRoute from './AdminProtectedRoutes'
import AdminLayout from '../layouts/AdminLayout'
import FreelancerProtectedRoute from './FreelancerProtectedRoutes'
import NotFound from '../components/NotFound'

const ClientRoutes = lazy(() => import("./ClientRoutes.jsx"));
const ResetPasswordPage = lazy(() => import("../features/client/resetpassword/ResetPasswordPage.jsx"));
const AdminRoutes = lazy(() => import("./AdminRoutes.jsx"));
const AdminLogin = lazy(() => import("../features/admin/auth/AdminLogin.jsx"));
const Landingpage = lazy(() => import("../features/client/landingPage/Landingpage.jsx"));
const FreelancerRoutes = lazy(() => import("./FreelancerRoutes.jsx"));
const ContactPage = lazy(() => import("../components/common/ContactUs.jsx"));
const WhyUsPage = lazy(() => import("../components/common/WhyUs.jsx"));
const SupportPage = lazy(() => import("../components/common/SupportPage.jsx"));
const AboutPage = lazy(() => import("../components/common/AboutUs.jsx"));
import { BrowserRouter, Route, Routes } from 'react-router-dom'

function AppRoutes() {
  
  return (
    
    <BrowserRouter>
      <ErrorBoundary label="This page">
      <Suspense fallback={<RouteFallback />}>
      <Routes>
<Route path="/404" element={<NotFound />} />
        <Route element={<RootLayout />}>

          <Route path="/admin/login" element={<AdminLogin />} />
          <Route path="/reset-password" element={<ResetPasswordPage />} />
          <Route path='/contact-us' element={<ContactPage/>}/>
          <Route path='/why-us' element={<WhyUsPage/>}/>
          <Route path='/about-us' element={<AboutPage/>}/>
          
          <Route path='/freelancer/*' element={<FreelancerProtectedRoute><FreelancerRoutes/></FreelancerProtectedRoute> }/>
          <Route element={<AdminProtectedRoute />}>
            <Route path="/admin/*" element={<AdminLayout />}>
              <Route path="*" element={<AdminRoutes />} />
            </Route>
          </Route>
          <Route
            path="/*"
            element={
              <ProtectedRoute>
                <ClientRoutes />
              </ProtectedRoute>
            }
          />
          <Route path="*" element={<NotFound />} />
        </Route>
      </Routes>
    </Suspense>
      </ErrorBoundary>
    </BrowserRouter>

  )
}

export default AppRoutes