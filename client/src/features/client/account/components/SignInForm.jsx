import { useState } from "react";
import { useDispatch } from "react-redux";
import { loginUser } from "../../../../api/auth/authApi.js";
import { setCredentials } from "../auth/authslice.js";
import { signinSchema } from "../validation/authSchemas.js";
import InputBox from "./InputBox.jsx";

export default function SignInForm({ onClose }) {
  const dispatch = useDispatch();
  const [values, setValues] = useState({ email: "", password: "" });
  const [error, setError] = useState("");
  const [errors, setErrors] = useState({});
  const [pending, setPending] = useState(false);
  const update = (event) => setValues((current) => ({ ...current, [event.target.name]: event.target.value }));
  const submit = async (event) => {
    event.preventDefault();
    const result = signinSchema.safeParse(values);
    if (!result.success) { setErrors(Object.fromEntries(result.error.issues.map((issue) => [issue.path[0], issue.message]))); return; }
    setErrors({}); setError(""); setPending(true);
    try { const response = await loginUser(values); dispatch(setCredentials({ accessToken: response.accessToken ?? response.AccessToken, user: { id: response.userId ?? response.UserId, email: response.email ?? response.Email, fullName: response.fullName ?? response.FullName, role: response.role ?? response.Role } })); onClose(); } catch (requestError) { setError(requestError.response?.data?.detail ?? "Unable to sign in."); } finally { setPending(false); }
  };
  return <form className="auth-form" onSubmit={submit} noValidate><InputBox label="Email" name="email" type="email" value={values.email} onChange={update} autoComplete="email" error={errors.email} /><InputBox label="Password" name="password" type="password" value={values.password} onChange={update} autoComplete="current-password" error={errors.password} />{error && <div className="admin-form-error" role="alert">{error}</div>}<button className="button button--accent" type="submit" disabled={pending}>{pending ? "Signing in..." : "Sign in"}</button></form>;
}