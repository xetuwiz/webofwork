import { useState } from "react";
import { Loader2, ShieldCheck, Smartphone } from "lucide-react";
import toast from "react-hot-toast";
import { useSendPhoneOtp, useVerifyPhoneOtp } from "../onBoardingMutations.js";

export default function PhoneOtpModal({ isOpen, phone, onClose, onVerified }) {
  const [otp, setOtp] = useState("");
  const { mutateAsync: sendOtp, isPending: isSending } = useSendPhoneOtp();
  const { mutateAsync: verifyOtp, isPending: isVerifying } = useVerifyPhoneOtp();

  if (!isOpen) return null;

  const handleSend = async () => {
    if (!phone?.trim()) {
      toast.error("Enter a phone number first.");
      return;
    }

    await sendOtp({ phone });
    toast.success("Verification code sent.");
  };

  const handleVerify = async () => {
    if (!otp.trim()) {
      toast.error("Enter the OTP code.");
      return;
    }

    await verifyOtp({ phone, otp });
    toast.success("Phone verified.");
    onVerified?.();
    onClose();
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/40 p-4">
      <div className="w-full max-w-md rounded-2xl border border-slate-200 bg-white p-6 shadow-xl">
        <div className="mb-5 flex items-start justify-between gap-4">
          <div className="flex items-center gap-3">
            <div className="rounded-full bg-blue-50 p-2 text-blue-600">
              <Smartphone className="h-5 w-5" />
            </div>
            <div>
              <h3 className="text-lg font-bold text-slate-900">Verify phone</h3>
              <p className="text-sm text-slate-500">One-time passcode</p>
            </div>
          </div>
          <button type="button" onClick={onClose} className="text-sm text-slate-500 hover:text-slate-700">Close</button>
        </div>

        <div className="mb-4 rounded-xl border border-slate-200 bg-slate-50 p-3 text-sm text-slate-700">
          Sending OTP to <span className="font-semibold">{phone}</span>
        </div>

        <div className="space-y-4">
          <div>
            <label className="mb-1 block text-sm font-medium text-slate-700">OTP code</label>
            <input
              value={otp}
              onChange={(event) => setOtp(event.target.value)}
              placeholder="123456"
              className="w-full rounded-xl border border-slate-200 bg-white px-3 py-2.5 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100"
            />
          </div>

          <div className="flex items-center gap-3">
            <button
              type="button"
              onClick={handleSend}
              disabled={isSending}
              className="flex-1 rounded-xl border border-slate-200 bg-slate-100 px-3 py-2.5 text-sm font-medium text-slate-700 hover:bg-slate-200 disabled:cursor-not-allowed disabled:opacity-50"
            >
              {isSending ? <span className="inline-flex items-center justify-center gap-2"><Loader2 className="h-4 w-4 animate-spin" />Sending</span> : "Send code"}
            </button>
            <button
              type="button"
              onClick={handleVerify}
              disabled={isVerifying}
              className="flex-1 rounded-xl bg-blue-600 px-3 py-2.5 text-sm font-semibold text-white hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-60"
            >
              {isVerifying ? <span className="inline-flex items-center justify-center gap-2"><Loader2 className="h-4 w-4 animate-spin" />Verifying</span> : <span className="inline-flex items-center justify-center gap-2"><ShieldCheck className="h-4 w-4" />Verify</span>}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
