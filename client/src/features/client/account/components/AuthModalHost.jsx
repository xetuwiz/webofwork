import { useModal } from "../../../../hooks/modal/useModalStore.jsx";
import SignInModal from "../SignInModal.jsx";
import SignupModal from "../SignupModal.jsx";
import ContractOfferModal from "../../contract/components/ContractOfferModal.jsx";
import ApproveContractModal from "../../contract/components/ApproveContractModal.jsx";
import ClientDisputeModal from "../../contract/components/ClientDisputeModal.jsx";
import OfferModal from "../../../freelancer/offers/OfferModal.jsx";

export default function AuthModalHost() {
  const { modal, modalProps, closeModal } = useModal();
  if (modal === "signin") return <SignInModal onClose={closeModal} />;
  if (modal === "signup") return <SignupModal onClose={closeModal} />;
  if (modal === "contract-offer") return <ContractOfferModal {...modalProps} onClose={closeModal} />;
  if (modal === "approve-contract") return <ApproveContractModal {...modalProps} onClose={closeModal} />;
  if (modal === "client-dispute") return <ClientDisputeModal {...modalProps} onClose={closeModal} />;
  if (modal === "offer-modal") return <OfferModal {...modalProps} onClose={closeModal} />;
  return null;
}