import { useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { adminLoginSchema } from "./adminLoginSchema";
import { useDispatch, useSelector } from "react-redux";
import { ShieldCheck, Mail, Lock, Loader2, ArrowRight } from "lucide-react";
import { loginThunk, logoutThunk } from "../../client/account/auth/authThunks";
import { Navigate, useNavigate } from "react-router-dom";
import { logout } from "../../client/account/auth/authslice";
import { logoutUser } from "../../../api/auth/authApi";
import { toastError, toastSuccess } from "../../../utils/toast";
import Button from "../../../components/common/Button";
import PasswordInput from "../../../components/common/PasswordInput";
import { Field } from "../../../components/common/Field";

export default function AdminLogin() {
  const dispatch = useDispatch();
  const [serverError, setServerError] = useState("");
  const navigate = useNavigate()
  const { user, loading } = useSelector(state => state.auth)

  useEffect(() => {
    if (!loading && user?.is_admin) {
      navigate("/admin", { replace: true });
    }
  }, [user, loading, navigate]);

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm({
    resolver: zodResolver(adminLoginSchema),
  });

  const onSubmit = async (data) => {
    setServerError("");

    try {
      const res = await dispatch(loginThunk(data)).unwrap();

      if (!res.user?.is_admin) {
        setServerError("Access denied. You are not an administrator.");
        toastError("Access Denied")
        await logoutUser()
        dispatch(logout())
        return;
      }
      navigate('/admin', { replace: true })
      toastSuccess("Login Successful")

    } catch (err) {
      setServerError(
        err?.message || "Invalid admin credentials"
      );
      toastError(err?.message || "invalid admin credentials")
    }
  };

  return (
    <main id="main-content" tabIndex={-1} className="min-h-screen bg-surface-2 flex items-center justify-center px-4">
      <div className="w-full max-w-dialog-sm">

        <div className="flex flex-col items-center mb-8">
          <div className="w-14 h-14 bg-brand rounded-2xl flex items-center justify-center mb-4">
            <ShieldCheck className="text-white w-8 h-8" />
          </div>
          <h1 className="text-2xl font-bold text-fg">
            WebOfWork <span className="text-brand">Admin</span>
          </h1>
        </div>

        <div className="bg-surface rounded-2xl shadow p-8">
          <form onSubmit={handleSubmit(onSubmit)} className="space-y-5">

            {serverError && (
              <div className="text-sm text-danger bg-danger-soft border border-danger/30 p-3 rounded-lg">
                {serverError}
              </div>
            )}

            <div>
              {/* The Password field below this one already had an htmlFor; this
                  one did not, so the two adjacent fields on the same form were
                  labelled inconsistently and the email named nothing. */}
              <Field label="Email" error={errors.email?.message}>
                {(f) => (
                  <div className="relative mt-1">
                    <Mail className="absolute left-3 top-3.5 w-4 h-4 text-fg-subtle" aria-hidden="true" />
                    <input
                      {...register("email")}
                      type="email"
                      {...f}
                      className="w-full pl-10 pr-4 py-3 border rounded-xl"
                      placeholder="admin@webofwork.com"
                    />
                  </div>
                )}
              </Field>
            </div>

            <div>
              <label className="text-sm font-semibold text-fg-muted" htmlFor="admin-login-password">
                Password
              </label>
              {/* The hand-rolled toggle had no aria-label and no aria-pressed, so
                  a screen reader announced two identical unlabelled buttons. */}
              <div className="relative mt-1">
                <Lock className="absolute left-3 top-3.5 w-4 h-4 text-fg-subtle" aria-hidden="true" />
                <PasswordInput
                  id="admin-login-password"
                  {...register("password")}
                  className="w-full pl-10 py-3 border rounded-xl"
                  placeholder="••••••••"
                  aria-label="Password"
                />
              </div>
              {errors.password && (
                <p className="text-xs text-danger-soft-fg mt-1">
                  {errors.password.message}
                </p>
              )}
            </div>

            <Button
              type="submit"
              variant="primary"
              size="none"
              loading={isSubmitting}
              className="w-full py-3 rounded-xl"
            >
              Login as Admin
              <ArrowRight size={16} />
            </Button>
          </form>  
        </div>
      </div>
    </main>
  );
}
