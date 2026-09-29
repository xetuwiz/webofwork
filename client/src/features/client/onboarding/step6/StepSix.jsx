import React from 'react';
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useOnBoarding } from '../OnBoardingContext';
import OnboardingLayout from '../../../../layouts/OnBoardingLayout';
import { stepSixSchema } from "./stepSixSchema";
import { useNavigate } from 'react-router-dom';
import { useSubmitFreelancerOnboarding } from '../onBoardingMutations';
import { useDispatch, useSelector } from 'react-redux';
import { setUser } from '../../account/auth/authslice';
import { toastApiError, toastSuccess } from "../../../../utils/toast";
import { Field } from "../../../../components/common/Field";

export default function StepSix() {
  const { formData, updateFormData } = useOnBoarding();
  const navigate = useNavigate();
  const { mutateAsync, isPending } = useSubmitFreelancerOnboarding();
  const dispatch = useDispatch();
  const { user } = useSelector((state) => state.auth);
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm({
    resolver: zodResolver(stepSixSchema),
    defaultValues: {
      paymentDetails: {
        // Was hardcoded to 'freelancer@paypal.com', a fake address that is not a
        // real PayPal account. It was persisted to freelancer_payment_settings,
        // so PayoutService later tried to pay it and PayPal rejected the payout
        // with RECEIVER_UNKNOWN. Default to the signed-in user's own address so
        // the field is always a real, deliverable account.
        paypalEmail: formData.paymentDetails?.paypalEmail || user?.email || '',
        isConfirmed: formData.paymentDetails?.isConfirmed || false,
      }
    },
  });
  
  const onSubmit = async (data) => {
    const payload = {
      tagline: formData.tagline,
      bio: formData.bio,
      phone: formData.phone,
      primary_category: formData.primaryCategory,
      skills: formData.skills,
      experience_level: formData.experienceLevel,
      // Price must be a JSON string, not a number. The API registers
      // DecimalAsStringJsonConverter, so a bare number fails model binding with
      // 400 and the whole onboarding submission is rejected.
      packages: Object.fromEntries(
        Object.entries(formData.packages || {}).map(([tier, pkg]) => [
          tier,
          {
            ...pkg,
            price: Number(pkg.price).toFixed(2),
            // Must be delivery_time, not deliveryTime. The API applies
            // SnakeCaseLower globally, so the camelCase key bound to nothing
            // and DeliveryTime silently defaulted to 0 -- the response was
            // still 201, so the days were lost with no error anywhere.
            delivery_time: Number(pkg.deliveryTime) || 0,
            deliveryTime: undefined,
          },
        ])
      ),
      portfolios: formData.portfolios,
      // The API's JSON naming policy is snake_case, so the property must be
      // paypal_email. Sending paypalEmail bound nothing and the model binder
      // reported "The PaypalEmail field is required".
      payment_details: {
        paypal_email: data.paymentDetails.paypalEmail,
      },
    };

    try {
      await mutateAsync(payload);
      dispatch(setUser({
        ...user,
        is_freelancer: true,
        current_role: 'freelancer'
      }));
      toastSuccess("Profile completed successfully");
      navigate("/freelancer");
    } catch (err) {
      // Was a flat "Failed to complete onboarding", which hid every real cause
      // Surfaces the API's actual message -- 400 model binding and 401 unverified
      // phone are the expected failures here, and both are diagnosable only if
      // the server's wording survives.
      toastApiError(err, "Failed to complete onboarding");
    }
  };

  return (
    <OnboardingLayout 
      title="Payout Method" 
      subtitle="Where you will receive your earnings once a client approves your work."
    >
      <div className="w-full max-w-page-md mx-auto">
        <form id="onboarding-form" className="space-y-6" onSubmit={handleSubmit(onSubmit)}>
          
          <div className="flex flex-col gap-2">
            <Field
              label="PayPal Email Address"
              error={errors.paymentDetails?.paypalEmail?.message}
              hint="Payouts go to this exact address, so double-check it before continuing."
            >
              {(f) => (
                <input
                  type="email"
                  {...register("paymentDetails.paypalEmail")}
                  placeholder="you@example.com"
                  {...f}
                  className="h-[54px] px-5 rounded-xl border border-line bg-surface text-fg outline-none focus-visible:border-line-strong focus-visible:ring-2 focus-visible:ring-line"
                />
              )}
            </Field>
          </div>

          <div className="p-4 rounded-2xl border border-brand-line bg-brand-soft flex items-start gap-4">
            <div className="p-2 bg-surface rounded-lg shadow-sm">
              <svg className="w-5 h-5 text-brand" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
              </svg>
            </div>
            <p className="text-xs text-brand leading-relaxed pt-1">
              Once the client approves your work, your earnings are sent to this PayPal account automatically. You can change it any time from Payment Settings.
            </p>
          </div>

          <div className="space-y-2">
            <div className="flex items-center gap-3 p-1">
              <input
                type="checkbox"
                id="confirm-details"
                {...register("paymentDetails.isConfirmed")}
                className="w-5 h-5 rounded-md border-line-strong text-brand focus-visible:ring-primary cursor-pointer"
              />
              <label htmlFor="confirm-details" className="text-sm text-fg-muted font-bold cursor-pointer">
                I confirm this PayPal email is correct and active.
              </label>
            </div>
            {errors.paymentDetails?.isConfirmed && <p className="text-danger-soft-fg text-xs font-bold ml-8">{errors.paymentDetails.isConfirmed.message}</p>}
          </div>
        </form>
      </div>
    </OnboardingLayout>
  );
}