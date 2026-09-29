import { useForm } from "react-hook-form";
import InputBox from "../../account/components/InputBox";
import { useDispatch } from "react-redux";
import { loginThunk } from "../auth/authThunks";
import { useModal } from "../../../../hooks/modal/useModalStore";
import { useNavigate } from "react-router-dom";
import { useEffect, useState } from "react";
import { initGoogleButton } from "../helper/googleSignupHelper";
import { googleAuth } from "../../../../api/auth/authApi";
import { setCredentials } from "../auth/authslice";
import { zodResolver } from "@hookform/resolvers/zod";
import { signinSchema } from "../validation/authSchemas";
import { toastApiError, toastError, toastSuccess } from "../../../../utils/toast";

export default function SignInForm() {
  const dispatch = useDispatch();
  const { register, handleSubmit, formState: { errors } } = useForm({
    resolver: zodResolver(signinSchema)
  });
  const { closeModal, openModal } = useModal();
  const navigate = useNavigate();
  const [loginError, setLoginError] = useState(null);

  const onSubmit = async ({ email, password }) => {
    try {
      setLoginError(null);
      const result = await dispatch(loginThunk({ email, password })).unwrap();
      closeModal();
      toastSuccess('Login Successful');
      navigate(result.user.current_role === 'freelancer' ? "/freelancer" : "/");
    } catch (err) {
      setLoginError(err?.error || "Invalid email or password");
      toastError('Login failed. Please try again.');
    }
  };

  useEffect(() => {
    initGoogleButton("google-signup-btn", async (response) => {
      try {
        const data = await googleAuth(response.credential);
        dispatch(setCredentials({ user: data.user, accessToken: data.access }));
        closeModal();
        navigate(data.user.current_role === 'freelancer' ? "/freelancer" : "/");
        toastSuccess('Login Successful');
      } catch (err) {
        toastApiError(err, "Login Failed try again");
      }
    });
  }, [dispatch, navigate, closeModal]);

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="space-y-4 sm:space-y-6">
      {loginError && (
        <div className="bg-danger-soft border border-danger/30 p-3 rounded-xl">
          <p className="text-danger-soft-fg text-center text-xs sm:text-sm font-medium">{loginError}</p>
        </div>
      )}
      
      <div className="space-y-4">
        <InputBox
          label="Email Address"
          name="email"
          type="email"
          placeholder="Enter your email"
          register={register}
          error={errors.email}
        />

        <InputBox
          label="Password"
          name="password"
          placeholder="Enter your password"
          register={register}
          error={errors.password}
          passwordToggle
        />
      </div>

      <div className="flex justify-end mt-2">
        <button
          type="button"
          onClick={() => {
            closeModal();
            openModal("forgot-password");
          }}
          className="text-xs sm:text-sm text-brand hover:underline font-medium"
        >
          Forgot password?
        </button>
      </div>

      <button
        type="submit"
        className="w-full py-3.5 bg-brand hover:bg-brand-hover active:scale-[0.98] text-brand-fg rounded-full
        font-poppins text-lg sm:text-xl font-medium transition-all shadow-lg shadow-blue-100"
      >
        Sign in
      </button>

      <div className="flex items-center gap-4 py-2">
        <div className="flex-1 h-px bg-surface-inset"></div>
        <span className="font-poppins text-sm sm:text-lg font-medium text-fg-subtle uppercase tracking-wider">
          Or
        </span>
        <div className="flex-1 h-px bg-surface-inset"></div>
      </div>

      <div className="w-full flex justify-center py-2">
        <div id="google-signup-btn" className="w-full flex justify-center" />
      </div>
    </form>
  );
}