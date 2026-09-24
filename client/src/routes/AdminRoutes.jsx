import { Route, Routes } from "react-router-dom";
import RoutePlaceholder from "../components/RoutePlaceholder.jsx";
import NotFound from "../components/NotFound.jsx";
import AdminOverview from "../features/admin/dashboard/components/AdminOverview.jsx";
import CategoriesAndSkills from "../features/admin/categorymanagement/CategoriesAndSkills.jsx";
import AdminUserList from "../features/admin/usermanagement/AdminUserList.jsx";
import SupportTicketList from "../features/admin/support/SupportTicketList.jsx";
import SupportTicketDetail from "../features/admin/support/SupportTicketDetials.jsx";
import FAQManagement from "../features/admin/faq/FAQManagement.jsx";
import TermAndConditions from "../features/admin/termsandconditionsmanagement/TermAndConditions.jsx";
import PrivacyPolicy from "../features/admin/privacypolicymanagement/PrivacyPolicy.jsx";
import AdminSettings from "../features/admin/settings/AdminSettings.jsx";
import AdminDashboard from "../features/admin/dashboard/AdminDashboard.jsx";
import PaymentRelease from "../features/admin/payoutmanagement/PaymentRelease.jsx";
import AdminTransactions from "../features/admin/transactions/AdminTransactions.jsx";
import DisputeList from "../features/admin/disputemanagement/DisputeList.jsx";
import DisputeDetail from "../features/admin/disputemanagement/DisputeDetail.jsx";
import ReviewList from "../features/admin/reviews/ReviewList.jsx";

const pages = [
  ["users", "Users", "Manage platform accounts and access."],
  ["payment-release", "Payment release", "Review eligible payouts."],
  ["transactions", "Transactions", "Inspect platform payment activity."],
  ["disputes", "Disputes", "Review active marketplace disputes."],
  ["settings", "Settings", "Configure platform defaults."],
  ["support", "Support", "Respond to support requests."],
  ["terms-and-conditions", "Terms and conditions", "Manage public terms."],
  ["privacy-policy", "Privacy policy", "Manage public privacy content."],
  ["faq-management", "FAQ management", "Maintain frequently asked questions."],
];

export default function AdminRoutes() {
  return (
    <Routes>
      <Route index element={<AdminOverview />} />
      <Route path="users" element={<AdminUserList />} />
      <Route path="category-management" element={<CategoriesAndSkills />} />
      <Route path="payment-release" element={<PaymentRelease />} />
      <Route path="transactions" element={<AdminTransactions />} />
      <Route path="disputes" element={<DisputeList />} />
      <Route path="disputes/:id" element={<DisputeDetail />} />
      <Route path="reviews" element={<ReviewList />} />
      <Route path="support" element={<SupportTicketList />} />
      <Route path="support/:id" element={<SupportTicketDetail />} />
      <Route path="faq-management" element={<FAQManagement />} />
      <Route path="terms-and-conditions" element={<TermAndConditions />} />
      <Route path="privacy-policy" element={<PrivacyPolicy />} />
      <Route path="settings" element={<AdminSettings />} />
      <Route path="dashboard" element={<AdminDashboard />} />
      <Route path="*" element={<NotFound />} />
    </Routes>
  );
}