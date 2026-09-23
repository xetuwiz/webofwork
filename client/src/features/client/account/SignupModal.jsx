import AuthLayout from "./components/AuthLayout.jsx";
import SignupForm from "./components/SignupForm.jsx";

export default function SignupModal({ onClose }) {
  return <AuthLayout eyebrow="Join the workspace" title="Create your account" description="Bring clearer projects and better collaboration into focus." onClose={onClose}><SignupForm onClose={onClose} /></AuthLayout>;
}