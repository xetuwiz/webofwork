import { Route, Routes } from "react-router-dom";
import FreelancerLayout from "../layouts/FreelancerLayout.jsx";
import NotFound from "../components/NotFound.jsx";
import RoutePlaceholder from "../components/RoutePlaceholder.jsx";

const page = (title, description) => <RoutePlaceholder area="Freelancer" title={title} description={description} />;

export default function FreelancerRoutes() {
  return (
    <Routes>
      <Route element={<FreelancerLayout />}>
        <Route index element={page("Freelancer dashboard", "Your work queue and recommendations will appear here.")} />
        <Route path="jobs" element={page("Find jobs", "Browse projects that match your skills.")} />
        <Route path="jobs/:id" element={page("Job details", "Review a project before submitting a proposal.")} />
        <Route path="profile" element={page("Freelancer profile", "Manage your public professional profile.")} />
        <Route path="profile/edit" element={page("Edit profile", "Update your skills, bio, and availability.")} />
        <Route path="profile/earnings-overview" element={page("Earnings overview", "Review your earnings and transaction history.")} />
        <Route path="invitations" element={page("Invitations", "Review invitations from clients.")} />
        <Route path="my-proposals" element={page("My proposals", "Track proposals you have submitted.")} />
        <Route path="offers" element={page("Offers", "Review active contract offers.")} />
        <Route path="contracts" element={page("Contracts", "Manage active contracts and deliverables.")} />
        <Route path="contracts/:id" element={page("Contract details", "Review milestones, deliverables, and revisions.")} />
        <Route path="settings/payment" element={page("Payment settings", "Manage your payout details.")} />
        <Route path="support" element={page("Support", "Get help with your freelancer account.")} />
        <Route path="faq" element={page("FAQ", "Find answers to common questions.")} />
        <Route path="terms-and-conditions" element={page("Terms and conditions", "Read the platform terms.")} />
        <Route path="privacy-policies" element={page("Privacy policy", "Read how platform data is handled.")} />
        <Route path="*" element={<NotFound />} />
      </Route>
    </Routes>
  );
}