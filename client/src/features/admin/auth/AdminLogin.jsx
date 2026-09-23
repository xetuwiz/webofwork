import { useEffect, useState } from "react";
import { useDispatch, useSelector } from "react-redux";
import { Navigate, useNavigate } from "react-router-dom";
import { loginUser, logoutUser } from "../../../api/auth/authApi.js";
import { logout, setCredentials } from "../../client/account/auth/authslice.js";
import { validateAdminLogin } from "./adminLoginSchema.js";

export default function AdminLogin() {
  const dispatch = useDispatch();
  const navigate = useNavigate();
  const { user } = useSelector((state) => state.auth);
  const [values, setValues] = useState({ email: "", password: "" });
  const [errors, setErrors] = useState({});
  const [serverError, setServerError] = useState("");
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (user && String(user.role ?? user.Role) === "3") navigate("/admin", { replace: true });
  }, [navigate, user]);

  if (user && String(user.role ?? user.Role) === "3") return <Navigate to="/admin" replace />;

  const update = (event) => setValues((current) => ({ ...current, [event.target.name]: event.target.value }));
  const submit = async (event) => {
    event.preventDefault();
    const nextErrors = validateAdminLogin(values);
    setErrors(nextErrors);
    setServerError("");
    if (Object.keys(nextErrors).length) return;

    setSubmitting(true);
    try {
      const response = await loginUser(values);
      const role = response.role ?? response.Role;
      if (String(role) !== "3" && String(role).toLowerCase() !== "admin") {
        setServerError("These credentials do not belong to an administrator.");
        await logoutUser().catch(() => undefined);
        dispatch(logout());
        return;
      }
      dispatch(setCredentials({ accessToken: response.accessToken ?? response.AccessToken, user: { id: response.userId ?? response.UserId, email: response.email ?? response.Email, fullName: response.fullName ?? response.FullName, role } }));
      navigate("/admin", { replace: true });
    } catch (error) {
      setServerError(error.response?.data?.detail ?? error.response?.data?.message ?? "Unable to sign in with these credentials.");
    } finally {
      setSubmitting(false);
    }
  };

  return <main className="admin-auth-page"><section className="admin-auth-panel"><span className="app-shell__eyebrow">Restricted workspace</span><h1>Admin sign in</h1><p>Review platform health, accounts, and money movement from one quiet control room.</p><form onSubmit={submit} noValidate><label>Email<input name="email" type="email" value={values.email} onChange={update} autoComplete="email" />{errors.email && <small>{errors.email}</small>}</label><label>Password<input name="password" type="password" value={values.password} onChange={update} autoComplete="current-password" />{errors.password && <small>{errors.password}</small>}</label>{serverError && <div className="admin-form-error" role="alert">{serverError}</div>}<button className="button button--accent" type="submit" disabled={submitting}>{submitting ? "Checking access..." : "Enter admin workspace"}</button></form></section></main>;
}