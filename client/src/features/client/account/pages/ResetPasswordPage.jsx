import { useState } from "react";
import { useSearchParams } from "react-router-dom";
import { resetPassword } from "../../../../api/auth/authApi.js";
import InputBox from "../components/InputBox.jsx";

const passwordRules = [
  "At least 8 characters",
  "One uppercase letter",
  "One lowercase letter",
  "One number",
  "One special character",
];

export default function ResetPasswordPage() {
  const [searchParams] = useSearchParams();
  const [values, setValues] = useState({
    email: searchParams.get("email") ?? "",
    token: searchParams.get("token") ?? "",
    newPassword: "",
    confirmPassword: "",
  });
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");
  const [pending, setPending] = useState(false);

  const update = (event) => {
    const { name, value } = event.target;
    setValues((current) => ({ ...current, [name]: value }));
  };

  const handleSubmit = async (event) => {
    event.preventDefault();
    setError("");
    setSuccess("");

    if (!values.email || !values.token) {
      setError("The password reset email and token are required.");
      return;
    }

    if (values.newPassword.length < 8) {
      setError("Password must be at least 8 characters long.");
      return;
    }

    if (values.newPassword !== values.confirmPassword) {
      setError("Passwords do not match.");
      return;
    }

    setPending(true);

    try {
      await resetPassword({ email: values.email, token: values.token, newPassword: values.newPassword });
      setSuccess("Your password has been reset successfully.");
      setValues({ email: values.email, token: values.token, newPassword: "", confirmPassword: "" });
    } catch (requestError) {
      setError(requestError.response?.data?.error ?? "Unable to reset the password.");
    } finally {
      setPending(false);
    }
  };

  return (
    <div style={{ maxWidth: 560, margin: "48px auto", padding: "0 20px" }}>
      <div className="auth-shell">
        <h2>Reset password</h2>
        <form onSubmit={handleSubmit} noValidate>
          <InputBox label="Email" name="email" type="email" value={values.email} onChange={update} autoComplete="email" />
          <InputBox label="Reset token" name="token" value={values.token} onChange={update} autoComplete="one-time-code" />
          <InputBox label="New password" name="newPassword" type="password" value={values.newPassword} onChange={update} autoComplete="new-password" />
          <InputBox label="Confirm password" name="confirmPassword" type="password" value={values.confirmPassword} onChange={update} autoComplete="new-password" />

          <ul style={{ margin: "0 0 18px", paddingLeft: 18, color: "#475569" }}>
            {passwordRules.map((rule) => <li key={rule}>{rule}</li>)}
          </ul>

          {error && <div className="admin-form-error" role="alert">{error}</div>}
          {success && <div className="admin-form-success" role="status">{success}</div>}

          <button type="submit" className="button button--accent" disabled={pending}>
            {pending ? "Resetting..." : "Reset password"}
          </button>
        </form>
      </div>
    </div>
  );
}
