import { useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import InputBox from "./InputBox";
import {zodResolver} from '@hookform/resolvers/zod'
import { signupSchema } from "../validation/authSchemas";
import { sendOtp } from "../../../../api/auth/authApi";
import { useModal } from "../../../../hooks/modal/useModalStore";
import { resendOtp } from "../../../../api/auth/authApi";
import { toastError, toastSuccess, toastInfo } from "../../../../utils/toast";
import {
  rememberPendingSignup,
  getPendingSignupEmail,
  clearPendingSignup,
} from "../../../../utils/pendingSignup";

export default function SignupForm({ }) {
  
  const [loading, setLoading] = useState(false);
  const {openModal, setSignupData, signupData} = useModal()
  const [backendError, setBackendError] = useState("");
  // Set when we find a registration still alive in Redis from an earlier,
  // interrupted attempt. Drives the "resume" banner.
  const [pendingEmail, setPendingEmail] = useState(() => getPendingSignupEmail());
  const [resuming, setResuming] = useState(false);

  const {
    register,
    handleSubmit,
    watch,
    formState: { errors },reset,
  } = useForm({
    resolver: zodResolver(signupSchema),
  });

  const handleSignup = async (formData)=> {
     if (loading) return;
     setLoading(true);
    try {
      const res = await sendOtp(formData);
    rememberPendingSignup(formData.email);
    setPendingEmail(formData.email.trim().toLowerCase());
    setSignupData(formData);
    openModal("otp",{email:formData.email})
    toastSuccess("OTP Sent! Check your Email")
  } catch (err) {
  const data = err.response?.data;

  if (!data) {
    setBackendError("Something went wrong. Please try again.");
    return;
  }

  if (typeof data.error === "string") {
    setBackendError(data.error);
    return;
  }

  const firstKey = Object.keys(data)[0];
  const value = data[firstKey];

  if (Array.isArray(value)) {
    setBackendError(value[0]);
  } else if (typeof value === "string") {
    setBackendError(value);
  } else {
    setBackendError("Something went wrong. Please try again.");
  }
}finally {
    setLoading(false);
  }
};

  useEffect(() => {
    if (signupData) {
      reset(signupData);
    }
  }, [signupData, reset]);

  /**
   * Resend the code for an interrupted signup. resend-otp doubles as the
   * liveness probe: it 400s with "Registration expired" once the Redis record
   * has passed its 10-minute TTL, so one call tells us whether resuming is
   * even possible. No new endpoint needed.
   */
  const handleResume = async () => {
    if (!pendingEmail || resuming) return;
    setResuming(true);
    try {
      await resendOtp({ email: pendingEmail });
      setSignupData({ email: pendingEmail });
      openModal("otp", { email: pendingEmail });
      toastSuccess("A new OTP is on its way to your inbox.");
    } catch (err) {
      const data = err.response?.data;
      const message =
        data?.non_field_errors?.[0] || data?.error || "Could not resume that signup.";
      // The 10-minute window has passed, so stop offering to resume.
      if (data?.non_field_errors) {
        clearPendingSignup();
        setPendingEmail(null);
      }
      toastError(message);
    } finally {
      setResuming(false);
    }
  };

  /** Abandon the interrupted attempt and go back to a blank form. */
  const handleDiscard = () => {
    clearPendingSignup();
    setPendingEmail(null);
    reset();
    toastInfo("Start a new signup below.");
  };


  const password = watch("password");
  return (
    <form onSubmit={handleSubmit(handleSignup)} className="flex flex-col flex-1">
      {backendError && (
  <p className="text-danger-soft-fg text-sm my-2 text-center">{backendError}</p>
)}

      {pendingEmail && (
        <div className="rounded-xl border border-warning/30 bg-warning-soft p-4 mb-4">
          <p className="text-sm font-semibold text-warning-soft-fg">
            Finish your signup
          </p>
          <p className="text-xs text-warning-soft-fg mt-1 mb-3">
            We already sent a verification code to{" "}
            <span className="font-semibold">{pendingEmail}</span>. Verification
            codes expire after 5 minutes, but you have about 10 minutes to
            finish creating the account.
          </p>
          <div className="flex flex-wrap items-center gap-3">
            <button
              type="button"
              onClick={handleResume}
              disabled={resuming}
              className="px-4 py-2 text-sm font-semibold rounded-lg bg-warning text-warning-fg hover:bg-warning disabled:opacity-50"
            >
              {resuming ? "Sending..." : "Resend code"}
            </button>
            <button
              type="button"
              onClick={handleDiscard}
              className="px-4 py-2 text-sm font-semibold rounded-lg text-warning-soft-fg hover:bg-warning-soft"
            >
              Use a different account
            </button>
          </div>
        </div>
      )}

      <InputBox
        label="Email Address"
        name="email"
        type="email"
        placeholder="Enter your email"
        register={register}
        validation={{
          required: "Email is required",
          pattern: {
            value: /\S+@\S+\.\S+/,
            message: "Enter a valid email",
          },
        }}
        error={errors.email}
      />

      <InputBox
        label="Full Name"
        name="fullName"
        placeholder="Enter your full name"
        register={register}
        validation={{ required: "Full name is required" }}
        error={errors.fullName}
      />

      <InputBox
        label="Password"
        name="password"
        placeholder="Create your password"
        register={register}
        validation={{
          required: "Password is required",
          minLength: { value: 6, message: "At least 6 characters" },
        }}
        error={errors.password}
        passwordToggle
      />

      <InputBox
        label="Confirm Password"
        name="confirmPassword"
        placeholder="Re-enter your password"
        register={register}
        validation={{
          required: "Confirm password is required",
          validate: (value) =>
            value === watch("password") || "Passwords do not match",
        }}
        error={errors.confirmPassword}
        passwordToggle
      />

      <button onClick={() => handleSignup}
        type="submit"
        className="w-full h-[47px] rounded-[40px] bg-brand hover:bg-brand/90 font-poppins font-medium text-xl text-white mt-4"
      >
        {loading? "Sending OTP...":"Create an account"}
      </button>
      <div className="text-center text-sm text-fg-muted mt-5">
              Already have an account?{" "}
              <button onClick={()=>openModal('signin')} className="text-brand font-semibold hover:underline">
                Login
              </button>
            </div>
    </form>
  );
}