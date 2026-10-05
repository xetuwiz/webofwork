import ModalWrapper from './components/ModelWrapper'
import { useForm } from "react-hook-form";
import { useState } from "react";
import { useModal } from '../../../hooks/modal/useModalStore';
import { forgotPassword } from '../../../api/auth/authApi';
import { getErrorMessage } from '../../../utils/errorMessage';

function ForgotPasswordModal() {
  const { openModal, closeModal } = useModal();
  const { register, handleSubmit, formState: { errors } } = useForm();
  const [loading, setLoading] = useState(false);
  const [backendError, setBackendError] = useState("");
  const [successMessage, setSuccessMessage] = useState("");


  const onSubmit = async({email})=>{
    try{
      setBackendError("");
      setLoading(true);

      await forgotPassword({ email });
      setSuccessMessage(
  "If an account exists, a reset link has been sent to your email."
);
      
    }catch(err){
        setBackendError(getErrorMessage(err, "Something went wrong. Please try again."));
    } finally {
      setLoading(false);
    }
    }
    const toSignin = ()=>{
      closeModal()
      openModal('signin')
    }
  
  return (
    
    // Single-column form with one input, so it opts back down to `md`. The
    // wrapper's default is `xl` for the split-panel sign-in/sign-up screens,
    // and 1100px for a lone email field would just be a lot of empty panel.
    <ModalWrapper onClose={closeModal} title="Reset your password" size="md">
      {successMessage ? (
  <div className="text-center p-10 space-y-4">
    <h2 className="text-3xl font-extrabold text-brand">
      Check your email
    </h2>
    <p className="text-fg-muted">{successMessage}</p>

    <button
      onClick={toSignin}
      className="text-brand mt-5 underline"
    >
      Back to Login
    </button>
  </div>
) : (
  
  
  <div className="w-[538px] max-w-[90vw] rounded-[20px] p-0 border-0 bg-surface">
        <div className="flex justify-center items-center px-[45px] py-[58px]">
          <div className="w-full max-w-md flex flex-col gap-8">
            
            <div className="flex flex-col items-center">
              <h1 className="text-fg text-center  font-black text-[36px] leading-[45px] tracking-[-1.8px]">
                Forgot Your Password?
              </h1>
              <p className="text-fg-subtle text-center text-[16px] font-normal leading-6">
                Enter your registered email to receive a reset link.
              </p>
            </div>
            {backendError && (
              <p className="text-danger-soft-fg text-center text-sm">
                {backendError}
              </p>
            )}

            <div className="rounded-xl border border-line shadow-[0_1px_2px_0_rgba(0,0,0,0.05)] bg-surface p-6">
              <form onSubmit={handleSubmit(onSubmit)} className="flex flex-col gap-6">
                <div className="flex flex-col gap-2">
                  {/* Had `htmlFor="email"` but the <input> below carried no
                      matching `id`, so the label pointed at nothing. Adding the
                      id was the smaller fix than removing the htmlFor, and it
                      keeps the association explicit rather than relying on
                      nesting.

                      This is the case a label-only grep cannot see: the label
                      *looks* correct. It is also why the render harness asserts
                      that no `label[for]` dangles -- a label with an htmlFor is
                      normally assumed fine, and here it was not. */}
                  <label
                    htmlFor="forgot-password-email"
                    className="text-fg text-[16px] font-medium leading-6"
                  >
                    Email Address
                  </label>
                  <input
                    id="forgot-password-email"
                    type="email"
                    placeholder="e.g., yourname@example.com"
                    className="h-14 rounded-lg border px-4 focus-visible:ring-2 focus-visible:ring-brand"
                    {...register("email", {
                      required: "Email is required",
                      pattern: {
                        value: /\S+@\S+\.\S+/,
                        message: "Enter a valid email address",
                      },
                    })}
                    />

                  {errors.email && (
                    <p className="text-danger-soft-fg text-xs">
                      {errors.email.message}
                    </p>
                  )}
                </div>

                 <button
                  type="submit"
                  disabled={loading}
                  className="h-12 bg-brand hover:bg-brand-hover rounded-2xl text-white font-bold disabled:opacity-60"
                  >
                  {loading ? "Sending link..." : "Send Link"}
                </button>
              </form>
            </div>

            <div className="flex flex-col items-center">
              <button
                onClick={toSignin}
                type="button"
                className="text-fg-subtle text-[14px] underline hover:text-fg transition-colors"
                >
                Back to Login
              </button>
            </div>

          </div>
        </div>
      </div>
  
)}
                  </ModalWrapper>
  )
}

export default ForgotPasswordModal