import { Route, Routes } from "react-router-dom";
import { OnboardingProvider } from "../features/client/onboarding/OnBoardingContext.jsx";
import ClientLayout from "../layouts/ClientLayout.jsx";
import ClientProfileLayout from "../layouts/ClientProfileLayout.jsx";
import OnBoardingLayout from "../layouts/OnBoardingLayout.jsx";
import NotFound from "../components/NotFound.jsx";
import RoutePlaceholder from "../components/RoutePlaceholder.jsx";

const page = (title, description) => <RoutePlaceholder area="Client" title={title} description={description} />;

export default function ClientRoutes() {
  return (
    <Routes>
      <Route element={<ClientLayout />}>
        <Route index element={page("Client home", "Your marketplace workspace will appear here when the homepage feature is connected.")} />
        <Route path="find-talents" element={page("Find talent", "Search the freelancer marketplace by skills and fit.")} />
        <Route path="find-talents/:id" element={page("Freelancer profile", "Review a freelancer profile and portfolio.")} />
        <Route path="create-project" element={page("Create project", "Describe the work you need completed.")} />
        <Route path="my-projects" element={page("My projects", "Track your active and completed projects.")} />
        <Route path="my-projects/:id" element={page("Project details", "Review project requirements and proposals.")} />
        <Route path="contracts" element={page("Contracts", "Review client-side contracts and milestones.")} />
        <Route path="contracts/:id" element={page("Contract details", "Inspect contract status and delivery progress.")} />
        <Route path="onboarding" element={<OnboardingProvider><OnBoardingLayout><RoutePlaceholder area="Onboarding" title="Start your profile" description="The onboarding steps will be connected in Phase 19C." /></OnBoardingLayout></OnboardingProvider>} />
        <Route element={<ClientProfileLayout />}>
          <Route path="profile" element={page("Profile overview", "Review your client profile and marketplace activity.")} />
          <Route path="profile/settings" element={page("Account settings", "Manage account preferences and security.")} />
          <Route path="profile/interests" element={page("Interests", "Tune the work categories you care about.")} />
          <Route path="profile/edit" element={page("Edit profile", "Update your public client information.")} />
          <Route path="profile/spending" element={page("Spending summary", "Review your project spending history.")} />
        </Route>
        <Route path="how-it-works" element={page("How it works", "Understand the WebOfWork marketplace flow.")} />
        <Route path="support" element={page("Support", "Get help with your WebOfWork account.")} />
        <Route path="faq" element={page("FAQ", "Find answers to common questions.")} />
        <Route path="terms-and-conditions" element={page("Terms and conditions", "Read the platform terms.")} />
        <Route path="privacy-policies" element={page("Privacy policy", "Read how platform data is handled.")} />
        <Route path="*" element={<NotFound />} />
      </Route>
    </Routes>
  );
}