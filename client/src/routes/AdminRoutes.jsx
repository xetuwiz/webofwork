import { lazy, Suspense } from 'react';
import RouteFallback from '../components/common/RouteFallback';
import { ErrorBoundary } from '../components/common/ErrorBoundary';
import NotFound from '../components/NotFound'

const AdminDashboard = lazy(() => import("../features/admin/dashboard/AdminDashboard.jsx"));
const AdminUserList = lazy(() => import("../features/admin/usermanagement/AdminUserList.jsx"));
const CategoriesAndSkills = lazy(() => import("../features/admin/categorymanagement/CategoriesAndSkills.jsx"));
const PaymentRelease = lazy(() => import("../features/admin/payoutmanagement/PaymenRelease.jsx"));
const AdminTransactions = lazy(() => import("../features/admin/transactions/AdminTransactions.jsx"));
const DisputeList = lazy(() => import("../features/admin/disputemanagement/DisputeList.jsx"));
const DisputeDetail = lazy(() => import("../features/admin/disputemanagement/DisputeDetail.jsx"));
const AdminSettings = lazy(() => import("../features/admin/settings/AdminSettings.jsx"));
const SupportTicketList = lazy(() => import("../features/admin/support/SupportTicketList.jsx"));
const SupportTicketDetail = lazy(() => import("../features/admin/support/SupportTicketDetials.jsx"));
const TermsAndConditions = lazy(() => import("../features/admin/termsandconditionsmanagement/TermAndConditions.jsx"));
const PrivacyPolicy = lazy(() => import("../features/admin/privacypolicymanagement/PrivacyPolicy.jsx"));
const FAQManagement = lazy(() => import("../features/admin/faq/FAQManagement.jsx"));
  // Shared with the client and freelancer routes. An admin's own
  // notification settings were previously unreachable: the screen lived
  // under features/freelancer and was only routed from FreelancerRoutes.
  const NotificationPreferences = lazy(() => import("../features/shared/NotificationPreferences.jsx"));

// The AI run monitor, and the manager approval gate.
//
// This is not a telemetry view. A run reaches `PendingManagerApproval` only from
// a validate-stage run, and only an admin can move it from there -- so these two
// routes are the only place the second human-in-the-loop gate can be operated.
const AiWorkflowMonitor = lazy(() => import("../features/aiworkflow/AiWorkflowMonitor.jsx"));
const AiWorkflowDetail = lazy(() => import("../features/aiworkflow/AiWorkflowDetail.jsx"));

import { Route, Routes } from 'react-router-dom'

function AdminRoutes() {
  return (
    <>
    <ErrorBoundary label="This page">
      <Suspense fallback={<RouteFallback />}>
    <Routes>

        <Route index element={<AdminDashboard/>} />
        <Route path="users" element={<AdminUserList />} />
        <Route path='category-management' element={<CategoriesAndSkills/>}/>
        <Route path='payment-release' element={<PaymentRelease/>}/>
        <Route path='transactions' element={<AdminTransactions/>}/>
        <Route path='disputes' element={<DisputeList/>}/>
        <Route path='disputes/:id' element={<DisputeDetail/>}/>
        <Route path='settings/' element={<AdminSettings/>}/>
  <Route path='settings/notifications/' element={<NotificationPreferences/>}/>
        <Route path='support/' element={<SupportTicketList/>}/>
        <Route path='support/:id' element={<SupportTicketDetail/>}/>
        <Route path='terms-and-conditions/' element={<TermsAndConditions/>}/>
        <Route path='privacy-policy/' element={<PrivacyPolicy/>}/>
        <Route path='faq-management/' element={<FAQManagement/>}/>
        <Route path='ai-workflows' element={<AiWorkflowMonitor/>}/>
        <Route path='ai-workflows/:id' element={<AiWorkflowDetail/>}/>
        <Route path="*" element={<NotFound />} />
    </Routes>
    </Suspense>
      </ErrorBoundary>
    </>
  )
}

export default AdminRoutes
