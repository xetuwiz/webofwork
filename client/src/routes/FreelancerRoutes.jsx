import { Route, Routes } from "react-router-dom";
import FreelancerLayout from "../layouts/FreelancerLayout.jsx";
import NotFound from "../components/NotFound.jsx";
import FreelancerDashboard from "../features/freelancer/dashboard/FreelancerDashboard.jsx";
import Profile from "../features/freelancer/profile/Profile.jsx";
import ProfileEdit from "../features/freelancer/profile/ProfileEdit.jsx";
import PaymentSettings from "../features/freelancer/profile/PaymentSettings.jsx";
import EarningsOverview from "../features/freelancer/profile/EarningsOverview.jsx";
import MyProposals from "../features/freelancer/proposals/components/MyProposals.jsx";
import FindJobs from "../features/freelancer/findJobs/FindJobs.jsx";
import JobDetails from "../features/freelancer/jobdetails/JobDetails.jsx";
import InvitationsPage from "../features/freelancer/invitations/InvitationsPage.jsx";

export default function FreelancerRoutes() {
  return (
    <Routes>
      <Route element={<FreelancerLayout />}>
        <Route index element={<FreelancerDashboard />} />
        <Route path="jobs" element={<FindJobs />} />
        <Route path="jobs/:id" element={<JobDetails />} />
        <Route path="profile" element={<Profile />} />
        <Route path="profile/edit" element={<ProfileEdit />} />
        <Route path="profile/earnings-overview" element={<EarningsOverview />} />
        <Route path="invitations" element={<InvitationsPage />} />
        <Route path="my-proposals" element={<MyProposals />} />
        <Route path="offers" element={<div className="p-8 text-gray-500">Offers are not implemented in this phase.</div>} />
        <Route path="contracts" element={<div className="p-8 text-gray-500">Contracts are not implemented in this phase.</div>} />
        <Route path="contracts/:id" element={<div className="p-8 text-gray-500">Contract detail view is not implemented in this phase.</div>} />
        <Route path="settings/payment" element={<PaymentSettings />} />
        <Route path="support" element={<div className="p-8 text-gray-500">Support is not implemented in this phase.</div>} />
        <Route path="faq" element={<div className="p-8 text-gray-500">FAQ is not implemented in this phase.</div>} />
        <Route path="terms-and-conditions" element={<div className="p-8 text-gray-500">Terms and conditions are not implemented in this phase.</div>} />
        <Route path="privacy-policies" element={<div className="p-8 text-gray-500">Privacy policy is not implemented in this phase.</div>} />
        <Route path="*" element={<NotFound />} />
      </Route>
    </Routes>
  );
}