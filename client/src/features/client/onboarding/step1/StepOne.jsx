import OnboardingLayout from "../../../../layouts/OnBoardingLayout";
import { stepOneSchema } from "./stepOneSchema";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useOnBoarding } from "../OnBoardingContext";
import { useFreelancerProfile } from "../onBoardingQueries";
import { useSendPhoneOtp } from "../onBoardingMutations";
import { useModal } from "../../../../hooks/modal/useModalStore";
import { Field } from "../../../../components/common/Field";
import { Link } from "react-router-dom";
import { normalizeSriLankanPhone } from "../../../../utils/sriLankanPhone";
import { toastError, toastSuccess, toastWarning } from "../../../../utils/toast";

export default function StepOne() {
  const { formData, updateFormData, nextStep } = useOnBoarding();
  const { openModal } = useModal();

  const { data: freelancerProfile } = useFreelancerProfile();
  const { mutateAsync: sendOtp, isPending } = useSendPhoneOtp();



  const {
    register,
    handleSubmit,
    watch,
    formState: { errors },
  } = useForm({
    resolver: zodResolver(stepOneSchema),
    defaultValues: {
      ...formData,
      phone:freelancerProfile?.phone
    },
  });

  const phoneValue = watch("phone");

   // Compare normalized forms, not raw strings. The stored number is always
   // +947XXXXXXXX, so "0779091086" typed again must still count as verified.
   const phoneVerified =
    freelancerProfile?.is_phone_verified === true &&
    freelancerProfile?.phone != null &&
    normalizeSriLankanPhone(phoneValue) === normalizeSriLankanPhone(freelancerProfile.phone);
const handleVerify = async () => {
  if (!phoneValue) return;

 try {
    // Send the canonical E.164 form so the number stored matches the number
    // verified, regardless of how the user typed it.
    await sendOtp({ phone: normalizeSriLankanPhone(phoneValue) });
    toastSuccess("OTP sent successfully!");
    openModal("phone-otp", { phone: phoneValue });
  } catch (err) {
   const data = err.response?.data;
    let errorMessage = "Failed to send OTP. Please try again.";

    if (data) {
      if (typeof data === 'string') {
        errorMessage = data;
      } else if (data.phone) {
        errorMessage = data.phone[0];  
      } else if (data.error) {
        errorMessage = data.error;
      } else if (data.detail) {
        errorMessage = data.detail; 
      }
    }
    toastError(errorMessage);
  }
};


const onSubmit = (data) => {
  if (!phoneVerified) {
    toastWarning("Please verify your phone number before continuing.");
    return;
  }

  updateFormData(data);
  nextStep();
};


  return (
    <OnboardingLayout 
      title="Tell Us About Yourself" 
      subtitle="Let clients know who you are and what you bring to the table."
    >
      <form
        id="onboarding-form"
        onSubmit={handleSubmit(onSubmit)}
        className="space-y-8"
      >
        <div className="space-y-2">
          {/* Was a <label> closed above its input with no htmlFor and no id on
              the input, so it named nothing. <Field> generates the id, wires
              label/control/message, and turns the validation <p> into a live
              region so a message appearing after submit is actually announced. */}
          <Field label="Tagline" error={errors.tagline?.message}>
            {(f) => (
              <input
                {...register("tagline")}
                type="text"
                placeholder="Full-stack Developer | UI/UX Enthusiast"
                {...f}
                className={`w-full h-[54px] px-5 rounded-xl border transition-all text-base outline-none focus-visible:ring-2 focus-visible:ring-primary/20 ${
                  errors.tagline
                    ? "border-red-500"
                    : "border-line-strong focus-visible:border-primary"
                }`}
              />
            )}
          </Field>
        </div>

        <div className="space-y-2">
          <Field label="Short Bio" error={errors.bio?.message}>
            {(f) => (
              <textarea
                {...register("bio")}
                placeholder="Tell clients about your experience..."
                {...f}
                className={`w-full min-h-[140px] px-5 py-4 rounded-xl border transition-all text-base resize-none outline-none focus-visible:ring-2 focus-visible:ring-primary/20 ${
                  errors.bio
                    ? "border-red-500"
                    : "border-line-strong focus-visible:border-primary"
                }`}
              />
            )}
          </Field>
        </div>

        <div className="space-y-2">
          {/* One input plus a Verify button. The button is a submit action, not
              a second choice in a group, so this stays a Field -- wrapping both
              in a fieldset would imply the user picks between a phone number
              and a button. */}
          <Field label="Phone Number" error={errors.phone?.message}>
            {(f) => (
              <div className="flex gap-3 flex-col sm:flex-row">
                <input
                  {...register("phone")}
                  type="tel"
                  placeholder="0779091086"
                  {...f}
                  className={`h-[54px] min-h-11 px-5 rounded-xl border transition-all text-base flex-1 outline-none focus-visible:ring-2 focus-visible:ring-primary/20 ${
                    errors.phone
                      ? "border-red-500"
                      : "border-line-strong focus-visible:border-primary"
                  }`}
                />

                {!phoneVerified && (
                  <button
                    type="button"
                    onClick={handleVerify}
                    disabled={isPending}
                    className="h-[54px] px-6 rounded-xl bg-inverse text-white text-sm font-bold hover:bg-black transition-colors disabled:opacity-50"
                  >
                    Verify
                  </button>
                )}
              </div>
            )}
          </Field>

          {phoneVerified && (
            <p className="text-success text-xs font-semibold ml-1 mt-1">
                Phone number verified
            </p>
          )}
        </div>

        <div className="space-y-2">
          <div className="flex items-start gap-4 pt-4">
            <input
              {...register("agreedToTerms")}
              id="agreedToTerms"
              type="checkbox"
              className="mt-1.5 w-5 h-5 accent-primary border-line-strong rounded cursor-pointer"
            />
            <label
              htmlFor="agreedToTerms"
              className="text-sm text-fg-muted leading-relaxed cursor-pointer"
            >
              I have read and agree to the{" "}
              <Link
                to="/terms-and-conditions"
                className="text-brand font-bold hover:underline"
              >
                Terms & Conditions
              </Link>{" "}
              and{" "}
              <Link
                to="/privacy-policies"
                className="text-brand font-bold hover:underline"
              >
                Privacy Policy
              </Link>
              .
            </label>
          </div>
          {errors.agreedToTerms && (
            <p className="text-danger-soft-fg text-xs font-medium">
              {errors.agreedToTerms.message}
            </p>
          )}
        </div>
      </form>
    </OnboardingLayout>
  );
}
