import { useModal } from "../../../../hooks/modal/useModalStore.jsx";
import SignInModal from "../SignInModal.jsx";
import SignupModal from "../SignupModal.jsx";

export default function AuthModalHost() {
  const { modal, closeModal } = useModal();
  if (modal === "signin") return <SignInModal onClose={closeModal} />;
  if (modal === "signup") return <SignupModal onClose={closeModal} />;
  return null;
}