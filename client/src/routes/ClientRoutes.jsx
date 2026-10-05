import { lazy, Suspense } from 'react';
import RouteFallback from '../components/common/RouteFallback';
import { ErrorBoundary } from '../components/common/ErrorBoundary';
import ClientLayout from "../layouts/ClientLayout";
import ClientProfileLayout from "../layouts/ClientProfileLayout";
import NotFound from "../components/NotFound";

const ClientHomepage = lazy(() => import("../features/client/homepage/ClientHomepage.jsx"));
const AddProject = lazy(() => import("../features/client/projectManagement/AddProject.jsx"));
const MyProjects = lazy(() => import("../features/client/projectManagement/MyProjects.jsx"));
const EditProject = lazy(() => import("../features/client/projectManagement/EditProject.jsx"));
const ProposalsIndex = lazy(() => import("../features/client/projectManagement/proposals/ProposalsPage.jsx"));
const ProfileOverview = lazy(() => import("../features/client/profile/ProfileOverview.jsx"));
const AccountSettings = lazy(() => import("../features/client/profile/AccountSettings.jsx"));
const Interests = lazy(() => import("../features/client/profile/Interests.jsx"));
const ProfileEdit = lazy(() => import("../features/client/profile/EditProfile.jsx"));
const OnboardingManager = lazy(() => import("../features/client/onboarding/OnboardingManager.jsx"));
const FindTalent = lazy(() => import("../features/client/findtalent/FindTalent.jsx"));
const FreelancerProfile = lazy(() => import("../features/client/freelancerprofile/FreelancerProfile.jsx"));
const ClientContracts = lazy(() => import("../features/client/contract/ClientContracts.jsx"));
const ClientContractDetail = lazy(() => import("../features/client/contract/ContractDetail.jsx"));
const ClientJobDetail = lazy(() => import("../features/client/projectManagement/ProjectDetails.jsx"));
const SpendingSummary = lazy(() => import("../features/client/profile/SpendingSummary.jsx"));
const HowItWorks = lazy(() => import("../features/client/howitworks/HowItWorks.jsx"));
const SupportPage = lazy(() => import("../components/common/SupportPage.jsx"));
const TermsAndConditions = lazy(() => import("../components/common/termsandconditions/TermsAndConditions.jsx"));
const PrivacyPolicies = lazy(() => import("../components/common/privacypolicy/PrivacyPolicies.jsx"));
const FAQPage = lazy(() => import("../components/common/faq/FAQ.jsx"));
const AllFreelancerReviews = lazy(() => import("../features/client/freelancerprofile/AllFreelancerReviews.jsx"));
const AiShortlistPage = lazy(() => import("../features/aiworkflow/AiShortlistPage.jsx"));
  // Shared with the freelancer and admin routes. This screen used to be
  // freelancer-only, so a client had no way to reach their own settings.
  const NotificationPreferences = lazy(() => import("../features/shared/NotificationPreferences.jsx"));
import { Route, Routes } from "react-router-dom";

import { useSelector } from "react-redux";

import { OnBoardingProvider } from "../features/client/onboarding/OnBoardingContext";

export default function ClientRoutes() {
  const { user,loading } = useSelector(state => state.auth);
  if (loading) {
    return null; 
  }
  return (
    <ErrorBoundary label="This page">
      <Suspense fallback={<RouteFallback />}>
      <Routes>
      
        <Route element={<ClientLayout />}>
          <Route path="/" element={<ClientHomepage />} />
          <Route path="/find-talents" element={<FindTalent/>}/>
          <Route path="/find-talents/:id" element={<FreelancerProfile/>}/>
          <Route path='/find-talents/:id/reviews' element={<AllFreelancerReviews/>}/>
          <Route path="/create-project" element={<AddProject/>}/>
          <Route path="/my-projects" element={<MyProjects/>}/>
          <Route path="/my-projects/:id" element={<ClientJobDetail/>}/>
          <Route path="/my-projects/:id/edit" element={<EditProject/>}/>
          <Route path="/my-projects/:id/proposals" element={<ProposalsIndex/>}/>
          {/* The shortlist approval gate. The workflow run id is in the route
              rather than looked up from the project, so two clients on the same
              project never share one page -- and one person's pending approval
              never appears in another's browser. */}
          <Route path="/my-projects/:id/ai-shortlist/:workflowRunId" element={<AiShortlistPage/>}/>
          <Route element={<ClientProfileLayout/>}>
          <Route path="/profile" element={<ProfileOverview/>}/>
          <Route path="/profile/settings" element={<AccountSettings/>}/>
  <Route path="/settings/notifications" element={<NotificationPreferences/>}/>
          <Route path="/profile/interests" element={<Interests/>}/>
          <Route path="/profile/edit" element={<ProfileEdit/>}/>
          <Route path="/profile/spending" element={<SpendingSummary/>}/>
          </Route>
          <Route path="/onboarding" element={ <OnBoardingProvider><OnboardingManager /></OnBoardingProvider>} />
          <Route path="/contracts" element={<ClientContracts/>}/>
          <Route path="/contracts/:id" element={<ClientContractDetail/>}/>
          <Route path="/how-it-works" element={<HowItWorks/>}/>
          <Route path='/support' element={<SupportPage/>}/>
          <Route path="/terms-and-conditions" element={<TermsAndConditions/>}/>
          <Route path="/privacy-policies" element={<PrivacyPolicies/>}/>
          <Route path="/faq" element={<FAQPage/>}/>
          <Route path='*' element={<NotFound/>}/>
        </Route>
          
    </Routes>
     </Suspense>
      </ErrorBoundary>
  );
}
