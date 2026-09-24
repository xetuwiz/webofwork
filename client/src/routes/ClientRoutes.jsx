import { Route, Routes } from "react-router-dom";
import { OnboardingProvider } from "../features/client/onboarding/OnBoardingContext.jsx";
import ClientLayout from "../layouts/ClientLayout.jsx";
import ClientProfileLayout from "../layouts/ClientProfileLayout.jsx";
import OnBoardingLayout from "../layouts/OnBoardingLayout.jsx";
import NotFound from "../components/NotFound.jsx";
import RoutePlaceholder from "../components/RoutePlaceholder.jsx";
import ClientContracts from "../features/client/contract/ClientContracts.jsx";
import ClientContractDetail from "../features/client/contract/ContractDetail.jsx";

import AboutUs from "../components/common/AboutUs.jsx";
import ContactUs from "../components/common/ContactUs.jsx";
import WhyUs from "../components/common/WhyUs.jsx";
import FAQ from "../components/common/faq/FAQ.jsx";
import PrivacyPolicies from "../components/common/privacypolicy/PrivacyPolicies.jsx";
import TermsAndConditions from "../components/common/termsandconditions/TermsAndConditions.jsx";
import Chat from "../components/chat/Chat.jsx";
import Notifications from "../components/notifications/Notifications.jsx";
import Landingpage from "../features/client/landingPage/Landingpage.jsx";
import ClientHomepage from "../features/client/homepage/ClientHomepage.jsx";
import HowItWorks from "../features/client/howitworks/HowItWorks.jsx";
import ClientProtectedRoute from "./ClientProtectedRoute.jsx";
import AccountSettings from "../features/client/profile/AccountSettings.jsx";
import ProfileOverview from "../features/client/profile/ProfileOverview.jsx";
import EditProfile from "../features/client/profile/EditProfile.jsx";
import Interests from "../features/client/profile/Interests.jsx";
import SpendingSummary from "../features/client/profile/SpendingSummary.jsx";
import OnboardingManager from "../features/client/onboarding/OnboardingManager.jsx";
import FreelancerProfile from "../features/client/freelancerprofile/FreelancerProfile.jsx";
import AllFreelancerReviews from "../features/client/freelancerprofile/AllFreelancerReviews.jsx";

const page = (title, description) => <RoutePlaceholder area="Client" title={title} description={description} />;

export default function ClientRoutes() {
  return (
    <Routes>
      <Route index element={<Landingpage />} />
      <Route path="about-us" element={<AboutUs />} />
      <Route path="contact-us" element={<ContactUs />} />
      <Route path="why-us" element={<WhyUs />} />
      <Route path="faq" element={<FAQ />} />
      <Route path="privacy-policies" element={<PrivacyPolicies />} />
      <Route path="terms-and-conditions" element={<TermsAndConditions />} />
      <Route path="how-it-works" element={<HowItWorks />} />
      <Route path="home" element={<ClientHomepage />} />
      <Route element={<ClientLayout />}>
        <Route path="find-talents" element={<FindTalent />} />
        <Route path="find-talents/:id" element={<FreelancerProfile />} />
        <Route path="find-talents/:id/reviews" element={<AllFreelancerReviews />} />
        <Route path="create-project" element={<AddProject />} />
        <Route path="my-projects" element={<MyProjects />} />
        <Route path="my-projects/:id" element={<ProjectDetails />} />
        <Route path="my-projects/:id/edit" element={<EditProject />} />
        <Route path="my-projects/:id/proposals" element={<ProposalsPage />} />
        <Route path="contracts" element={<ClientContracts />} />
        <Route path="contracts/:id" element={<ClientContractDetail />} />
        <Route path="chat" element={<Chat />} />
        <Route path="notifications" element={<Notifications />} />
        <Route path="onboarding" element={<OnboardingProvider><OnBoardingLayout><OnboardingManager /></OnBoardingLayout></OnboardingProvider>} />
        <Route element={<ClientProtectedRoute />}>
          <Route element={<ClientProfileLayout />}>
            <Route path="profile" element={<ProfileOverview />} />
            <Route path="profile/settings" element={<AccountSettings />} />
            <Route path="profile/interests" element={<Interests />} />
            <Route path="profile/edit" element={<EditProfile />} />
            <Route path="profile/spending" element={<SpendingSummary />} />
          </Route>
        </Route>
        <Route path="support" element={page("Support", "Get help with your WebOfWork account.")} />
        <Route path="*" element={<NotFound />} />
      </Route>
    </Routes>
  );
}