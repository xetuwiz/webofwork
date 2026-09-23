import { useState } from "react";
import { deleteAccount, requestDeleteOtp } from "../../../../api/auth/authApi.js";
import InputBox from "./InputBox.jsx";

export default function DeleteAccountModal({ isOpen, onClose }) {
  const [otp, setOtp] = useState("");
  const [confirmText, setConfirmText] = useState("");
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");
  const [pending, setPending] = useState(false);
  const [otpRequested, setOtpRequested] = useState(false);

  if (!isOpen) {
    return null;
  }

  const handleRequestOtp = async () => {
    setError("");
    setSuccess("");
    setPending(true);

    try {
      await requestDeleteOtp();
      setOtpRequested(true);
      setSuccess("A one-time code has been sent to your email.");
    } catch (requestError) {
      setError(requestError.response?.data?.error ?? "Unable to send the deletion code.");
    } finally {
      setPending(false);
    }
  };

  const handleDelete = async (event) => {
    event.preventDefault();
    setError("");
    setSuccess("");

    if (!otp.trim()) {
      setError("Enter the one-time code sent to your email.");
      return;
    }

    if (confirmText.trim() !== "DELETE") {
      setError("Type DELETE to confirm account deletion.");
      return;
    }

    setPending(true);

    try {
      await deleteAccount({ otp });
      setSuccess("Account deleted successfully.");
      onClose();
    } catch (requestError) {
      setError(requestError.response?.data?.error ?? "The account deletion request was rejected.");
    } finally {
      setPending(false);
    }
  };

  return (
    <div style={{ position: "fixed", inset: 0, background: "rgba(15, 23, 42, 0.65)", display: "grid", placeItems: "center", padding: 20, zIndex: 40 }}>
      <div style={{ width: "min(520px, 100%)", background: "#fff", borderRadius: 16, padding: 24, boxShadow: "0 20px 60px rgba(15, 23, 42, 0.18)" }}>
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 16 }}>
          <h3 style={{ margin: 0, color: "#991b1b" }}>Delete account</h3>
          <button type="button" className="button button--secondary" onClick={onClose}>Close</button>
        </div>

        <p style={{ marginTop: 0, color: "#475569" }}>This action is permanent and cannot be undone.</p>

        <div style={{ display: "flex", justifyContent: "space-between", gap: 12, marginBottom: 16 }}>
          <button type="button" className="button button--secondary" onClick={handleRequestOtp} disabled={pending || otpRequested}>
            {otpRequested ? "Code sent" : "Send delete code"}
          </button>
        </div>

        <form onSubmit={handleDelete} noValidate>
          <InputBox label="One-time code" name="otp" value={otp} onChange={(event) => setOtp(event.target.value)} inputMode="numeric" />
          <InputBox label="Type DELETE to confirm" name="confirmText" value={confirmText} onChange={(event) => setConfirmText(event.target.value)} />
          {error && <div className="admin-form-error" role="alert">{error}</div>}
          {success && <div className="admin-form-success" role="status">{success}</div>}
          <div style={{ display: "flex", justifyContent: "flex-end", gap: 12, marginTop: 18 }}>
            <button type="button" className="button button--secondary" onClick={onClose}>Cancel</button>
            <button type="submit" className="button button--danger" disabled={pending}>{pending ? "Deleting..." : "Delete account"}</button>
          </div>
        </form>
      </div>
    </div>
  );
}
