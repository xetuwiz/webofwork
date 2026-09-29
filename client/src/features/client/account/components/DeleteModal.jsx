import { useState } from "react";
import { useDispatch, useSelector } from "react-redux";
import { deleteAccount, requestDeleteOTP } from "../auth/authThunks";
import { AlertTriangle, Loader2, ShieldAlert, X, Mail } from "lucide-react";
import Dialog from "../../../../components/common/Dialog";
import { toastError, toastSuccess, toastInfo } from "../../../../utils/toast";
import Button from "../../../../components/common/Button";
import { Field } from "../../../../components/common/Field";

export default function DeleteAccountModal({ onClose }) {
  const [inputValue, setInputValue] = useState("");
  const [localError, setLocalError] = useState(null);
  const [isDeleting, setIsDeleting] = useState(false);
  const [isSendingOTP, setIsSendingOTP] = useState(false);
  const [otpSent, setOtpSent] = useState(false);
  
  const user = useSelector((state) => state.auth.user);
  const dispatch = useDispatch();

  const handleSendOTP = async () => {
    setIsSendingOTP(true);
    const result = await dispatch(requestDeleteOTP());
    setIsSendingOTP(false);
    
    if (requestDeleteOTP.fulfilled.match(result)) {
      setOtpSent(true);
      toastInfo("Verification code sent to your email");
    } else {
      toastError(result.payload?.error || "Failed to send code");
    }
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setIsDeleting(true);
    setLocalError(null);

    const payload = user?.is_google_account 
      ? { otp: inputValue } 
      : { password: inputValue };

    const result = await dispatch(deleteAccount(payload));
    
    if (deleteAccount.fulfilled.match(result)) {
      onClose();
      window.location.href = "/";
      toastSuccess("Account Deleted Successfully");
    } else {
      const errorMsg = result.payload?.password || result.payload?.otp || "Action failed";
      setLocalError(errorMsg);
      setIsDeleting(false);
      toastError("Deletion Failed");
    }
  };

  return (
    <Dialog
            open
            onClose={onClose}
      title="Delete Account?"
      size="md"
      className="overflow-hidden"
    >
        <div className="relative p-8 md:p-10">
          <button 
            onClick={onClose}
            className="absolute right-6 top-6 p-2 text-fg-subtle hover:text-fg hover:bg-surface-3 rounded-full transition-all"
          >
            <X className="w-5 h-5" />
          </button>

          <div className="flex items-center justify-center w-20 h-20 bg-danger-soft rounded-3xl mb-8 mx-auto rotate-3">
            <ShieldAlert className="w-10 h-10 text-danger -rotate-3" />
          </div>
          
          <div className="text-center mb-8">
            <h2 className="text-2xl md:text-3xl font-black text-fg mb-3 tracking-tight">Delete Account?</h2>
            <p className="text-sm md:text-base text-fg-subtle font-medium leading-relaxed">
              This action is <span className="text-danger font-bold underline decoration-2 underline-offset-4">permanent</span>.
            </p>
          </div>

          <form onSubmit={handleSubmit} className="space-y-6">
            {!user?.is_google_account ? (
              <div className="space-y-2">
                <Field label="Confirm Password" error={localError}>
                  {(f) => (
                    <input
                      type="password"
                      required
                      value={inputValue}
                      onChange={(e) => setInputValue(e.target.value)}
                      {...f}
                      className={`w-full px-5 py-4 rounded-2xl border ${localError ? 'border-red-500 bg-danger-soft/30' : 'border-line bg-surface-2'} focus-visible:ring-4 focus-visible:ring-danger/10 focus-visible:border-danger outline-none transition-all font-medium`}
                      placeholder="••••••••••••"
                    />
                  )}
                </Field>
              </div>
            ) : (
              <div className="space-y-4">
                {!otpSent ? (
                  <Button
                    type="button"
                    onClick={handleSendOTP}
                    variant="secondary"
                    size="none"
                    loading={isSendingOTP}
                    className="w-full py-4 bg-brand-soft text-brand border-brand-line rounded-2xl hover:bg-brand-hover-soft"
                  >
                    <Mail className="w-5 h-5" />
                    Send Verification Code
                  </Button>
                ) : (
                  <div className="space-y-2">
                    <Field label="Enter 6-Digit Code">
                      {(f) => (
                        <input
                          type="text"
                          inputMode="numeric"
                          autoComplete="one-time-code"
                          required
                          maxLength={6}
                          value={inputValue}
                          onChange={(e) => setInputValue(e.target.value.replace(/\D/g, "").slice(0, 6))}
                          {...f}
                          onPaste={(e) => {
                            // maxLength truncates a paste before onChange, so a code
                            // pasted with a leading space lost its last digit.
                            e.preventDefault();
                            setInputValue(e.clipboardData.getData("text").replace(/\D/g, "").slice(0, 6));
                          }}
                          className="w-full px-5 py-4 rounded-2xl border border-line bg-surface-2 text-center text-2xl font-black tracking-[0.5em] focus-visible:ring-4 focus-visible:ring-danger/10 focus-visible:border-danger outline-none transition-all"
                          placeholder="000000"
                        />
                      )}
                    </Field>
                    <button
                      type="button"
                      onClick={handleSendOTP}
                      className="text-[10px] font-bold text-brand uppercase tracking-widest hover:underline"
                    >
                      Resend Code?
                    </button>
                  </div>
                )}
              </div>
            )}

            {localError && (
              <p className="text-danger-soft-fg text-xs font-bold flex items-center gap-1.5 px-1">
                <AlertTriangle className="w-3 h-3" />
                {localError}
              </p>
            )}

            <div className="flex flex-col gap-3 pt-4">
              <Button
                type="submit"
                variant="danger"
                size="none"
                loading={isDeleting}
                disabled={isDeleting || (user?.is_google_account && !otpSent)}
                className="w-full py-4 font-black uppercase tracking-widest text-xs rounded-2xl active:scale-[0.97] shadow-xl shadow-red-200 gap-3"
              >
                Finalize Deletion
              </Button>
              <button
                type="button"
                onClick={onClose}
                className="w-full py-4 text-fg-subtle font-black uppercase tracking-widest text-xs rounded-2xl hover:bg-surface-2 transition-all"
              >
                Cancel
              </button>
            </div>
          </form>
        </div>
    </Dialog>
  );
}
