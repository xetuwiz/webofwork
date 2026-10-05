import { lazy, Suspense } from 'react';
import RouteFallback from '../components/common/RouteFallback';
import { ErrorBoundary } from '../components/common/ErrorBoundary';
import React from 'react'
import NotFound from '../components/NotFound'

const FreelancerDashboard = lazy(() => import("../features/freelancer/dashboard/FreelancerDashboard.jsx"));
const FindJobs = lazy(() => import("../features/freelancer/findJobs/FindJobs.jsx"));
const JobDetail = lazy(() => import("../features/freelancer/jobdetails/JobDetails.jsx"));
const Profile = lazy(() => import("../features/freelancer/profile/Profile.jsx"));
const ProfileEdit = lazy(() => import("../features/freelancer/profile/ProfileEdit.jsx"));
const InvitationsPage = lazy(() => import("../features/freelancer/invitations/InvitationsPage.jsx"));
const MyProposals = lazy(() => import("../features/freelancer/proposals/components/MyProposals.jsx"));
const FreelancerOffers = lazy(() => import("../features/freelancer/offers/FreelancerOffers.jsx"));
const Contracts = lazy(() => import("../features/freelancer/contracts/Contracts.jsx"));
const ContractDetail = lazy(() => import("../features/freelancer/contracts/ContractDetail.jsx"));
const EarningsOverview = lazy(() => import("../features/freelancer/profile/EarningsOverview.jsx"));
const PaymentSettings = lazy(() => import("../features/freelancer/profile/PaymentSettings.jsx"));
  // Shared across all three roles, and mounted in ClientRoutes and AdminRoutes
  // too. It used to live under `features/freelancer/profile` and be routed only
  // here, so clients and admins had no way to reach their own preferences.
  const NotificationPreferences = lazy(() => import("../features/shared/NotificationPreferences.jsx"));
const SupportPage = lazy(() => import("../components/common/SupportPage.jsx"));
const TermsAndConditions = lazy(() => import("../components/common/termsandconditions/TermsAndConditions.jsx"));
const PrivacyPolicies = lazy(() => import("../components/common/privacypolicy/PrivacyPolicies.jsx"));
const FAQPage = lazy(() => import("../components/common/faq/FAQ.jsx"));

import { Navigate, Route, Routes } from 'react-router-dom'
import { FreelancerLayout } from '../layouts/FreelancerLayout'

function FreelancerRoutes() {
  return (
    <ErrorBoundary label="This page">
      <Suspense fallback={<RouteFallback />}>
       <Routes>
        <Route element={<FreelancerLayout />}>
          <Route path='' element={<FreelancerDashboard />} />
        <Route path='/jobs' element={<FindJobs/>}/>
        <Route path='/jobs/:id' element={<JobDetail/>}/>
        <Route path='/profile' element={<Profile/>}/>
        <Route path='/profile/edit' element={<ProfileEdit/>}/>
        <Route path='/profile/earnings-overview' element={<EarningsOverview/>}/>
        <Route path='/invitations' element={<InvitationsPage/>}/>
        <Route path='/my-proposals' element = {<MyProposals/>}/>
        <Route path='/offers' element={<FreelancerOffers/>} />
        <Route path='/contracts' element={<Contracts/>}/>
        <Route path='/contracts/:id' element={<ContractDetail/>}/>
        <Route path='/settings/payment' element={<PaymentSettings/>}/>
        <Route path='/settings/notifications' element={<NotificationPreferences/>}/>
        <Route path='/support' element={<SupportPage/>}/>
        <Route path="/terms-and-conditions" element={<TermsAndConditions/>}/>
        <Route path='/privacy-policies' element={<PrivacyPolicies/>}/>
        <Route path="/faq" element={<FAQPage/>}/>
                  <Route path='*' element={<NotFound/>}/>
        
        </Route>
    </Routes>
       </Suspense>
      </ErrorBoundary>
)
}

export default FreelancerRoutes