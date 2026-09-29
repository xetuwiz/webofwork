import React, { useState, useEffect } from 'react';
import ModalWrapper from './components/ModelWrapper';
import { useModal } from '../../../hooks/modal/useModalStore';
import { verifyOtp } from '../../../api/auth/authApi';
import { useForm } from "react-hook-form";
import { useNavigate } from 'react-router-dom';
import { useDispatch } from 'react-redux';
import { setCredentials } from './auth/authslice';
import { clearPendingSignup } from '../../../utils/pendingSignup';
import { resendOtp } from "../../../api/auth/authApi";
import { toastApiError, toastError, toastSuccess } from "../../../utils/toast";
import { getErrorMessage } from "../../../utils/errorMessage";

export default function VerifyOtp({ email }) {
  const dispatch = useDispatch();
  const { closeModal, openModal } = useModal();
  const navigate = useNavigate();
  const [error,setError] = useState('')
  const [timer, setTimer] = useState(60);
  const [canResend, setCanResend] = useState(false);

  const {
    register,
    handleSubmit,
    setValue,
    watch,
    formState: { errors },
  } = useForm({ mode: "onChange" });

  const otpValue = watch("otp") || "";


  useEffect(() => {
    if (timer <= 0) {
      setCanResend(true);
      return;
    }

    const interval = setInterval(() => {
      setTimer((prev) => prev - 1);
    }, 1000);

    return () => clearInterval(interval);
  }, [timer]);

  const handleBack = () => openModal("signup");

  const handleVerify = async ({ otp }) => {
    try {
      const res = await verifyOtp({ email, otp_code: otp });


         // The account now exists, so the resume banner must not reappear.
         clearPendingSignup();
      dispatch(
        setCredentials({
          user: res.user,
          accessToken: res.access,
        })
      );

      closeModal();
      navigate("/");
      toastSuccess("Welcome to WebOfWork")
    } catch (err) {
       const message = getErrorMessage(err, "Invalid or expired OTP");

  setError(message);
  toastError(message)
    }
  };

  const handleResend = async () => {
    if (!canResend) return;

    try {
      await resendOtp({ email });
      setTimer(60);
      setCanResend(false);
      toastSuccess('Otp sent! check your email')
    } catch (err) {
      toastApiError(err, "Could not resend the OTP. Please wait a moment and try again.");
    }
  };

  return (
    // Single-column form; `md` is the right weight here even though the shared
    // wrapper now defaults to `xl` for the split-panel auth screens.
    <ModalWrapper onClose={closeModal} title="Verify your email" size="md">
      <form
        onSubmit={handleSubmit(handleVerify)}
        className="w-full max-w-md bg-surface rounded-xl shadow-2xl p-8 md:p-12 text-center relative"
      >
        <button
          type="button"
          onClick={handleBack}
          className="absolute top-4 left-4 p-2 text-fg-subtle hover:text-fg transition-colors"
        >
          <svg xmlns="http://www.w3.org/2000/svg" className="h-6 w-6"
               fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" d="M15 19l-7-7 7-7" />
          </svg>
        </button>

        <h1 className="text-3xl font-bold text-fg mb-4">
          Enter Verification Code
        </h1>

        <p className="text-fg-muted mb-8">
          We've sent a 6-digit code to{" "}
          <span className="font-semibold text-fg">{email}</span>
        </p>
        {error && (
  <p className="text-danger-soft-fg text-sm my-2 text-center">{ error}</p>
)}

        <input
          type="text"
          inputMode="numeric"
          autoComplete="one-time-code"
          maxLength={6}
          placeholder="Enter 6-digit code"
          {...register("otp", {
            required: "OTP is required",
            // minLength/maxLength on the options are only checked with mode:"onChange".
            // Without it react-hook-form never ran them, so a 5-digit code was posted
            // to the server and came back as a generic "Invalid OTP".
            minLength: { value: 6, message: "OTP must be 6 digits" },
            maxLength: { value: 6, message: "OTP must be 6 digits" },
            pattern: { value: /^[0-9]{6}$/, message: "OTP must be 6 digits" },
          })}
          onPaste={(e) => {
            // Pastes often carry spaces/dashes ("629 337"); keep digits only.
            const text = e.clipboardData.getData("text").replace(/\D/g, "").slice(0, 6);
            e.preventDefault();
            setValue("otp", text, { shouldValidate: true });
          }}
          className="w-full max-w-xs h-14 text-xl text-center font-medium tracking-[0.3em] border-2 rounded-lg border-line-strong focus-visible:border-brand focus-visible:ring-2 focus-visible:ring-brand/20 transition duration-fast"
        />

        {errors.otp && (
          <p className="text-danger-soft-fg text-xs mb-4">{errors.otp.message}</p>
        )}

        <button
          type="submit"
          disabled={!/^[0-9]{6}$/.test(otpValue)}
          className="w-full max-w-xs h-12 rounded-lg font-semibold text-white text-lg bg-brand hover:bg-brand/50 transition mt-4 disabled:opacity-50 disabled:cursor-not-allowed"
        >
          Verify OTP
        </button>

        <div className="mt-6 text-sm text-fg-muted">
          Didn't receive the code?

          <button
            type="button"
            onClick={handleResend}
            disabled={!canResend}
            className={`ml-2 font-semibold transition-colors  ${
              canResend
                ? "text-brand hover:text-brand"
                : "text-fg-subtle cursor-not-allowed"
            }`}
          >
            {canResend ? "Resend Code" : `Resend in ${timer}s`}
          </button>
        </div>
      </form>
    </ModalWrapper>
  );
}
