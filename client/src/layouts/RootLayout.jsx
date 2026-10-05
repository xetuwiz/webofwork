import { Outlet } from "react-router-dom";
import { useModal } from "../hooks/modal/useModalStore";

import { useDispatch, useSelector } from "react-redux";
import { useEffect, lazy, Suspense } from "react";
import { refreshSession } from "../features/client/account/auth/authThunks";
import { Loader2, X } from 'lucide-react'

import { useNotificationSocket } from "../hooks/notification/useNotificationSocket";
import SkipToContent from "../components/common/SkipToContent";
import { CommandPaletteHost } from "../components/common/CommandPalette";

const SignupModal = lazy(() => import("../features/client/account/SignupModal"));
const OtpModal = lazy(() => import("../features/client/account/OtpModal"));
const SignInModal = lazy(() => import("../features/client/account/SignInModal"));
const ForgotPasswordModal = lazy(() => import("../features/client/account/ForgotPasswordModal"));
const ConfirmProjectModal = lazy(() => import("../features/client/projectManagement/components/ConfirmProjectModal"));
const DeleteUserModal = lazy(() => import("../features/admin/usermanagement/components/DeleteUserModal"));
const SuspendUserModal = lazy(() => import("../features/admin/usermanagement/components/SuspendUserModal"));
const EditCategoryModal = lazy(() => import("../features/admin/categorymanagement/components/EditCategoryModal").then((m) => ({ default: m.EditCategoryModal })));
const AddCategoryModal = lazy(() => import("../features/admin/categorymanagement/components/AddCategoryModal").then((m) => ({ default: m.AddCategoryModal })));
const DeleteCategoryModal = lazy(() => import("../features/admin/categorymanagement/components/DeleteCategoryModal").then((m) => ({ default: m.DeleteCategoryModal })));
const AddSkillModal = lazy(() => import("../features/admin/categorymanagement/components/AddSkillsModal"));
const EditSkillModal = lazy(() => import("../features/admin/categorymanagement/components/EditSkillsModal"));
const DeleteSkillModal = lazy(() => import("../features/admin/categorymanagement/components/DeleteSkillModal"));
const ProfileModal = lazy(() => import("../features/client/homepage/components/ProfileModal"));
const DeleteAccountModal = lazy(() => import("../features/client/account/components/DeleteModal"));
const ProjectDeleteModal = lazy(() => import("../features/client/projectManagement/components/ProjectDeleteModal"));
const PhoneOtpModal = lazy(() => import("../features/client/onboarding/step1/PhoneOtpModal"));
const InviteFreelancerModal = lazy(() => import("../features/client/findtalent/components/InvitationModal"));
const ProposalModal = lazy(() => import("../features/freelancer/proposals/components/ProposalModal").then((m) => ({ default: m.ProposalModal })));
const ViewUserModal = lazy(() => import("../features/admin/usermanagement/components/ViewUserModal"));
const ContractOfferModal = lazy(() => import("../features/client/contract/components/ContractOfferModal"));
const ProposalDetailModal = lazy(() => import("../features/freelancer/proposals/components/ProposalDetailModal"));
const OfferModal = lazy(() => import("../features/freelancer/offers/OfferModal"));
const ApproveContractModal = lazy(() => import("../features/client/contract/components/ApproveContractModal"));
const PayoutReleaseModal = lazy(() => import("../features/admin/payoutmanagement/PayoutReleaseModal"));
const EditPaymentModal = lazy(() => import("../features/freelancer/profile/EditPaymentModal"));
const Notifications = lazy(() => import("../components/notifications/Notifications"));
const Chat = lazy(() => import("../components/chat/Chat"));
const DisputeModal = lazy(() => import("../features/freelancer/contracts/components/DisputeModal"));
const ClientDisputeModal = lazy(() => import("../features/client/contract/components/ClientDisputeModal"));
export default function RootLayout() {
  const { modal, modalProps, closeModal } = useModal();
  const { loading, initialized, user } = useSelector((s) => s.auth);
  const accessToken = useSelector((s) => s.auth.accessToken);
  const dispatch = useDispatch();

  const userId = useSelector((s) => s.auth.user?.id);
  
useNotificationSocket(userId);
  /**
   * Boot the session.
   *
   * The access token is short-lived (60 min) while the refresh cookie lasts 7
   * days, so "no access token in the store" does NOT mean "not signed in" -- it
   * normally means the tab was closed and reopened after the access token
   * expired. Calling `fetchUser` here instead of `refreshSession` is what logged
   * everyone out on every refresh: `fetchUser` needs a bearer token, had none,
   * got a 401, and the interceptor's reaction to that was `dispatch(logout())`,
   * which also wipes the persisted slice -- so the still-valid refresh cookie
   * was never given a chance.
   */
  useEffect(() => {
    if (!accessToken && !user) {
      dispatch(refreshSession());
    } else if (!accessToken) {
      dispatch({ type: 'auth/finishLoading' })
    }
  }, [accessToken, user, dispatch]);

  if (!initialized && accessToken) {
    return (
      <div className="h-screen flex items-center justify-center">
        <Loader2 className="w-8 h-8 text-brand animate-spin" />
      </div>
    );
  }
  return (
    <>
      {/* Rendered above the router so it is the first thing in the tab order on
          every route, including the public pages. See SkipToContent for why the
          link is here rather than in each layout. */}
      <SkipToContent />
      {/* Cmd/Ctrl+K, mounted above the router so the shortcut works on every
          route. Reads the role from the store and filters accordingly. */}
      <CommandPaletteHost />
      <Outlet />
      {/* Modals are lazy: they are each behind a `modal ===` guard, so the chunk is
        only requested when one is actually opened. */}
      <Suspense fallback={null}>{modal === "view-user" && (<ViewUserModal isOpen onClose={closeModal} user={modalProps.user} />)}
      {modal === "signup" && <SignupModal isOpen onClose={closeModal} />}
      {modal === "otp" && <OtpModal isOpen email={modalProps.email} />}
      {modal === "signin" && <SignInModal isOpen onClose={closeModal} />}
      {modal === "forgot-password" && <ForgotPasswordModal isOpen />}
      {modal === "confirm-project" && <ConfirmProjectModal isPending={false} onConfirm={modalProps.onConfirm} />}
      {modal === 'edit-category' && <EditCategoryModal open onOpenChange={closeModal} category={modalProps} />}
      {modal === 'edit-skill' && <EditSkillModal open onOpenChange={closeModal} skill={modalProps.item} categories={modalProps.allCategories} />}
      {modal === 'add-category' && <AddCategoryModal open onOpenChange={closeModal} />}
      {modal === 'add-skill' && <AddSkillModal categories={modalProps.allCategories?.data} onClose={closeModal} />}
      {modal === 'delete-category' && <DeleteCategoryModal open onOpenChange={closeModal} category={modalProps} />}
      {modal === 'delete-skill' && <DeleteSkillModal open onOpenChange={closeModal} skill={modalProps} />}
      {modal === "delete-user" && (<DeleteUserModal isOpen onClose={closeModal} modalProps={modalProps} />)}
      {modal === "suspend-user" && (<SuspendUserModal isOpen onClose={closeModal} modalProps={modalProps} />)}
      {modal === "profile-menu" && <ProfileModal isOpen onClose={closeModal} />}
      {modal === "delete-account" && <DeleteAccountModal onClose={closeModal} />}
      {modal === 'delete-project' && <ProjectDeleteModal projectId={modalProps.projectId} onClose={closeModal} />}
      {modal === 'phone-otp' && <PhoneOtpModal phone={modalProps.phone} />}
      {modal === 'job-invitation' && <InviteFreelancerModal isOpen onClose={closeModal} modalProps={modalProps} />}
      {modal === 'job-proposal' && <ProposalModal onClose={closeModal} projectId={modalProps} />}
      {modal === 'contract-offer' && <ContractOfferModal modalProps={modalProps} />}
      {modal === 'my-proposal-details' && <ProposalDetailModal isOpen onClose={closeModal} proposal={modalProps.proposal} />}
      {modal === 'offer-modal' && <OfferModal />}
      {modal === 'approve-contract' && <ApproveContractModal modalProps={modalProps} />}
      {modal === 'release-payout' && <PayoutReleaseModal isOpen onClose={closeModal} contract={modalProps} />}
      {modal === 'edit-payment' && <EditPaymentModal isOpen onClose={closeModal} currentEmail={modalProps.currentEmail} />}
      {modal === 'notifications' && <>
        <div
          className="fixed inset-0 bg-black/20 backdrop-blur-[2px] z-overlay"
          onClick={closeModal}
        />
        <div className="fixed top-[73px] right-4 md:right-8 z-popover">
          <Notifications onClose={closeModal} />
        </div>
      </>}
      {modal === 'messages' && <Chat onClose={closeModal} />}
      {modal === 'raise-dispute' && <DisputeModal onClose={closeModal} contractId={modalProps.contractId}/>}
      {modal === 'client-raise-dispute' && <ClientDisputeModal onClose={closeModal} contractId={modalProps.contractId} />}</Suspense>
    </>
  );
}

