import { useState } from "react";
import { useDispatch } from "react-redux";
import { verifyOtp } from "../../../api/auth/authApi.js";
import { setCredentials } from "./auth/authslice.js";
import InputBox from "./components/InputBox.jsx";

export default function OtpModal({ isOpen, email = "", password = "", fullName = "", role = 1, onClose, onSuccess }) {
  const dispatch = useDispatch();
  const [otp, setOtp] = useState("");
  const [error, setError] = useState("");
  const [pending, setPending] = useState(false);

  if (!isOpen) {
    return null;
  }

  const handleSubmit = async (event) => {
    event.preventDefault();
    setError("");

    if (!otp.trim()) {
      setError("Enter the verification code.");
      return;
    }

    setPending(true);

    try {
      const response = await verifyOtp({ email, otp, password, fullName, role });
      dispatch(setCredentials({
        accessToken: response.accessToken ?? response.AccessToken,
        user: {
          id: response.userId ?? response.UserId,
          email: response.email ?? response.Email,
          fullName: response.fullName ?? response.FullName,
          role: response.role ?? response.Role,
        },
      }));
      onSuccess?.(response);
      onClose?.();
    } catch (requestError) {
      setError(requestError.response?.data?.detail ?? "That verification code was not accepted.");
    } finally {
      setPending(false);
    }
  };

  return (
    <div style={{ position: "fixed", inset: 0, background: "rgba(15, 23, 42, 0.65)", display: "grid", placeItems: "center", padding: 20 }}>
      <div style={{ width: "min(420px, 100%)", background: "#fff", borderRadius: 16, padding: 24, boxShadow: "0 20px 60px rgba(15, 23, 42, 0.18)" }}>
        <h3 style={{ marginTop: 0 }}>Verify your account</h3>
        <form onSubmit={handleSubmit} noValidate>
          <InputBox label="Verification code" name="otp" value={otp} onChange={(event) => setOtp(event.target.value)} inputMode="numeric" autoComplete="one-time-code" />
          {error && <div className="admin-form-error" role="alert">{error}</div>}
          <div style={{ display: "flex", justifyContent: "flex-end", gap: 12, marginTop: 18 }}>
            <button type="button" className="button button--secondary" onClick={onClose}>Cancel</button>
            <button type="submit" className="button button--accent" disabled={pending}>{pending ? "Verifying..." : "Verify"}</button>
          </div>
        </form>
      </div>
    </div>
  );
}
