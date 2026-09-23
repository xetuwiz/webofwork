import AuthLayout from "./AuthLayout.jsx";
import SignInForm from "./SignInForm.jsx";

export default function SigninLayout({ onClose }) {
  return <AuthLayout eyebrow="Welcome back" title="Sign in to WebOfWork" description="Pick up where the work left off." onClose={onClose}><SignInForm onClose={onClose} /></AuthLayout>;
}