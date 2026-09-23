import { Route, Routes } from "react-router-dom";
import RoutePlaceholder from "../components/RoutePlaceholder.jsx";
import NotFound from "../components/NotFound.jsx";
import AdminOverview from "../features/admin/dashboard/components/AdminOverview.jsx";
import CategoriesAndSkills from "../features/admin/categorymanagement/CategoriesAndSkills.jsx";
import AdminUserList from "../features/admin/usermanagement/AdminUserList.jsx";

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
      {pages.filter(([path]) => path !== "users").map(([path, title, description]) => <Route key={path} path={path} element={<RoutePlaceholder area="Admin" title={title} description={description} />} />)}
      <Route path="*" element={<NotFound />} />
    </Routes>
  );
}