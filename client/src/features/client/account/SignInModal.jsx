import AuthLayout from "./components/AuthLayout.jsx";
import SignInForm from "./components/SignInForm.jsx";

export default function SignInModal({ onClose }) {
  return <AuthLayout eyebrow="Welcome back" title="Sign in to WebOfWork" description="Pick up where the work left off." onClose={onClose}><SignInForm onClose={onClose} /></AuthLayout>;
}