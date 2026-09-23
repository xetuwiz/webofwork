import { useState } from "react";
import { forgotPassword } from "../../../../api/auth/authApi.js";
import InputBox from "./InputBox.jsx";

export default function ForgotPasswordModal({ isOpen, initialEmail = "", onClose }) {
  const [email, setEmail] = useState(initialEmail);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");
  const [pending, setPending] = useState(false);

  if (!isOpen) {
    return null;
  }

  const handleSubmit = async (event) => {
    event.preventDefault();
    setError("");
    setSuccess("");

    if (!email.trim()) {
      setError("Enter the email connected to your account.");
      return;
    }

    setPending(true);

    try {
      await forgotPassword({ email });
      setSuccess("If the account exists, a reset link has been sent.");
    } catch (requestError) {
      setError(requestError.response?.data?.error ?? "Unable to send the reset email.");
    } finally {
      setPending(false);
    }
  };

  return (
    <div style={{ position: "fixed", inset: 0, background: "rgba(15, 23, 42, 0.6)", display: "grid", placeItems: "center", padding: 20, zIndex: 40 }}>
      <div style={{ width: "min(480px, 100%)", background: "#fff", borderRadius: 16, padding: 24, boxShadow: "0 20px 60px rgba(15, 23, 42, 0.18)" }}>
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 16 }}>
          <h3 style={{ margin: 0 }}>Forgot password</h3>
          <button type="button" className="button button--secondary" onClick={onClose}>Close</button>
        </div>

        <form onSubmit={handleSubmit} noValidate>
          <InputBox label="Email" name="email" type="email" value={email} onChange={(event) => setEmail(event.target.value)} autoComplete="email" />
          {error && <div className="admin-form-error" role="alert">{error}</div>}
          {success && <div className="admin-form-success" role="status">{success}</div>}
          <div style={{ display: "flex", justifyContent: "flex-end", gap: 12, marginTop: 18 }}>
            <button type="button" className="button button--secondary" onClick={onClose}>Cancel</button>
            <button type="submit" className="button button--accent" disabled={pending}>{pending ? "Sending..." : "Send reset email"}</button>
          </div>
        </form>
      </div>
    </div>
  );
}
