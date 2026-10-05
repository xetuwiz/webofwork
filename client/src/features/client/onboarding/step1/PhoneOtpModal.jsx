import { useState } from "react";
import { useVerifyPhoneOtp, useSendPhoneOtp } from "../onBoardingMutations";
import { useModal } from "../../../../hooks/modal/useModalStore";
import Dialog from "../../../../components/common/Dialog";
import { toastApiError, toastError, toastSuccess } from "../../../../utils/toast";

export default function PhoneOtpModal({ phone }) {
  const [otp, setOtp] = useState("");
  const { closeModal } = useModal();
  const { mutateAsync: verifyOtp, isPending } = useVerifyPhoneOtp();
  const { mutateAsync: resendOtp, isPending: isResending } = useSendPhoneOtp();

  const handleResend = async () => {
    try {
      await resendOtp({ phone });
      toastSuccess("A new OTP has been sent by SMS.");
    } catch (error) {
      toastApiError(error, "Could not resend the OTP. Please wait a moment and try again.");
    }
  };

  const handleVerify = async () => {
    if (otp.length !== 6) {
      toastError("Please enter a valid 6-digit OTP");
      return;
    }

    try {
      await verifyOtp({ phone, otp });
      toastSuccess("Phone verified successfully!");
      closeModal();
    } catch (error) {
      toastApiError(error, "Invalid OTP. Please try again.");
    }
  };

  return (
    <Dialog
            open
            onClose={closeModal}
      title="Verify Phone Number"
      size="md"
    >
        <h2 className="text-2xl font-extrabold text-fg mb-2">
          Verify Phone Number
        </h2>
        <p className="text-sm text-fg-subtle mb-6">
          {/* The backend sends this code by SMS to the phone number
              (PhoneVerificationService -> SendPhoneOtpJob -> SMS gateway).
              This used to name the user's email, which sent people looking in
              their inbox for a code that never arrived there. */}
          Enter the 6-digit OTP sent by SMS to{" "}
          <span className="font-semibold text-fg">{phone || "your phone"}</span>
        </p>

        <input
          type="text"
          inputMode="numeric"
          autoComplete="one-time-code"
          maxLength={6}
          value={otp}
          onChange={(e) => setOtp(e.target.value.replace(/\D/g, "").slice(0, 6))}
          onPaste={(e) => {
            // maxLength truncates a pasted value *before* onChange runs, so a code
            // pasted as " 629337" became "62933". Strip first, then set.
            e.preventDefault();
            setOtp(e.clipboardData.getData("text").replace(/\D/g, "").slice(0, 6));
          }}
          placeholder="Enter OTP"
          className="w-full h-[54px] px-5 rounded-xl border border-line-strong text-lg tracking-widest text-center outline-none focus-visible:ring-2 focus-visible:ring-primary"
        />

        <button
          onClick={handleVerify}
          disabled={isPending}
          className="mt-6 w-full h-[54px] rounded-xl bg-brand text-brand-fg font-bold hover:opacity-90 disabled:opacity-50"
        >
          {isPending ? "Verifying..." : "Verify OTP"}
        </button>

        <div className="mt-4 flex items-center justify-between gap-3">
          <button
            onClick={handleResend}
            disabled={isResending}
            className="text-sm font-semibold text-brand hover:underline disabled:opacity-50 disabled:no-underline"
          >
            {isResending ? "Sending..." : "Resend OTP"}
          </button>

          <button
            onClick={closeModal}
            className="text-sm font-semibold text-fg-subtle hover:text-fg-muted"
          >
            Cancel
          </button>
        </div>
    </Dialog>
  );
}
