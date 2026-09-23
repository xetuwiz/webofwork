import { useState } from "react";
import { sendOtp, verifyOtp } from "../../../../api/auth/authApi.js";
import { useDispatch } from "react-redux";
import { setCredentials } from "../auth/authslice.js";
import { signupSchema } from "../validation/authSchemas.js";
import InputBox from "./InputBox.jsx";

export default function SignupForm({ onClose }) {
  const dispatch = useDispatch();
  const [values, setValues] = useState({ fullName: "", email: "", password: "", confirmPassword: "", role: 1, otp: "" });
  const [stage, setStage] = useState("details");
  const [error, setError] = useState("");
  const [pending, setPending] = useState(false);
  const update = (event) => setValues((current) => ({ ...current, [event.target.name]: event.target.value }));
  const submitDetails = async (event) => { event.preventDefault(); const result = signupSchema.safeParse(values); if (!result.success) { setError(result.error.issues[0]?.message ?? "Check your details."); return; } setPending(true); setError(""); try { await sendOtp({ email: values.email }); setStage("otp"); } catch (requestError) { setError(requestError.response?.data?.detail ?? "Unable to send verification code."); } finally { setPending(false); } };
  const submitOtp = async (event) => { event.preventDefault(); setPending(true); setError(""); try { const response = await verifyOtp(values); dispatch(setCredentials({ accessToken: response.accessToken ?? response.AccessToken, user: { id: response.userId ?? response.UserId, email: response.email ?? response.Email, fullName: response.fullName ?? response.FullName, role: response.role ?? response.Role } })); onClose(); } catch (requestError) { setError(requestError.response?.data?.detail ?? "That verification code was not accepted."); } finally { setPending(false); } };
  if (stage === "otp") return <form className="auth-form" onSubmit={submitOtp} noValidate><InputBox label="Verification code" name="otp" inputMode="numeric" value={values.otp} onChange={update} error={error} /><button className="button button--accent" type="submit" disabled={pending}>{pending ? "Creating account..." : "Verify and create account"}</button></form>;
  return <form className="auth-form" onSubmit={submitDetails} noValidate><InputBox label="Full name" name="fullName" value={values.fullName} onChange={update} autoComplete="name" /><InputBox label="Email" name="email" type="email" value={values.email} onChange={update} autoComplete="email" /><InputBox label="Password" name="password" type="password" value={values.password} onChange={update} autoComplete="new-password" /><InputBox label="Confirm password" name="confirmPassword" type="password" value={values.confirmPassword} onChange={update} autoComplete="new-password" />{error && <div className="admin-form-error" role="alert">{error}</div>}<button className="button button--accent" type="submit" disabled={pending}>{pending ? "Sending code..." : "Continue"}</button></form>;
}