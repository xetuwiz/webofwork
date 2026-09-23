import AuthLayout from "./AuthLayout.jsx";
import SignupForm from "./SignupForm.jsx";

export default function SignupLayout({ onClose }) {
  return <AuthLayout eyebrow="Join the workspace" title="Create your account" description="Bring clearer projects and better collaboration into focus." onClose={onClose}><SignupForm onClose={onClose} /></AuthLayout>;
}