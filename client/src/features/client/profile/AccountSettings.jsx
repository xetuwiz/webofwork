import { useState } from "react";
import { useSelector } from "react-redux";
import { changePassword } from "./profileApi.js";
import ForgotPasswordModal from "../account/components/ForgotPasswordModal.jsx";
import DeleteAccountModal from "../account/components/DeleteAccountModal.jsx";

const initialPasswordValues = {
  currentPassword: "",
  newPassword: "",
  confirmPassword: "",
};

export default function AccountSettings() {
  const user = useSelector((state) => state.auth.user);
  const [passwordValues, setPasswordValues] = useState(initialPasswordValues);
  const [passwordError, setPasswordError] = useState("");
  const [passwordSuccess, setPasswordSuccess] = useState("");
  const [pending, setPending] = useState(false);
  const [showForgotModal, setShowForgotModal] = useState(false);
  const [showDeleteModal, setShowDeleteModal] = useState(false);

  const updatePassword = (event) => {
    const { name, value } = event.target;
    setPasswordValues((current) => ({ ...current, [name]: value }));
  };

  const handlePasswordSubmit = async (event) => {
    event.preventDefault();
    setPasswordError("");
    setPasswordSuccess("");

    if (!passwordValues.currentPassword || !passwordValues.newPassword) {
      setPasswordError("Current and new passwords are required.");
      return;
    }

    if (passwordValues.newPassword !== passwordValues.confirmPassword) {
      setPasswordError("The new passwords do not match.");
      return;
    }

    if (passwordValues.newPassword.length < 8) {
      setPasswordError("New password must be at least 8 characters long.");
      return;
    }

    setPending(true);

    try {
      await changePassword({
        currentPassword: passwordValues.currentPassword,
        newPassword: passwordValues.newPassword,
      });

      setPasswordSuccess("Password updated successfully.");
      setPasswordValues(initialPasswordValues);
    } catch (requestError) {
      setPasswordError(requestError.response?.data?.error ?? "Unable to update your password.");
    } finally {
      setPending(false);
    }
  };

  return (
    <div style={{ display: "grid", gap: 20 }}>
      <section style={{ background: "#fff", borderRadius: 16, padding: 24, boxShadow: "0 10px 35px rgba(15, 23, 42, 0.06)" }}>
        <h3 style={{ marginTop: 0 }}>Account security</h3>
        <p style={{ marginTop: 0, color: "#475569" }}>Signed in as {user?.email ?? "your account"}.</p>

        <form onSubmit={handlePasswordSubmit} noValidate>
          <div style={{ display: "grid", gap: 16 }}>
            <label className="auth-field">
              Current password
              <input name="currentPassword" type="password" value={passwordValues.currentPassword} onChange={updatePassword} autoComplete="current-password" />
            </label>
            <label className="auth-field">
              New password
              <input name="newPassword" type="password" value={passwordValues.newPassword} onChange={updatePassword} autoComplete="new-password" />
            </label>
            <label className="auth-field">
              Confirm new password
              <input name="confirmPassword" type="password" value={passwordValues.confirmPassword} onChange={updatePassword} autoComplete="new-password" />
            </label>
          </div>

          {passwordError && <div className="admin-form-error" role="alert">{passwordError}</div>}
          {passwordSuccess && <div className="admin-form-success" role="status">{passwordSuccess}</div>}

          <div style={{ display: "flex", gap: 12, flexWrap: "wrap", marginTop: 18 }}>
            <button type="submit" className="button button--accent" disabled={pending}>{pending ? "Updating..." : "Update password"}</button>
            <button type="button" className="button button--secondary" onClick={() => setShowForgotModal(true)}>Forgot password</button>
          </div>
        </form>
      </section>

      <section style={{ background: "#fff", borderRadius: 16, padding: 24, boxShadow: "0 10px 35px rgba(15, 23, 42, 0.06)" }}>
        <h3 style={{ marginTop: 0, color: "#7f1d1d" }}>Danger zone</h3>
        <p style={{ color: "#475569" }}>Delete this account permanently. A confirmation code will be requested before the removal proceeds.</p>
        <button type="button" className="button button--danger" onClick={() => setShowDeleteModal(true)}>Delete account</button>
      </section>

      <ForgotPasswordModal isOpen={showForgotModal} initialEmail={user?.email ?? ""} onClose={() => setShowForgotModal(false)} />
      <DeleteAccountModal isOpen={showDeleteModal} onClose={() => setShowDeleteModal(false)} />
    </div>
  );
}
