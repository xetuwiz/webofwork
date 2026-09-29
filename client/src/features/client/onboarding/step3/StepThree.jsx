import React from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import OnboardingLayout from '../../../../layouts/OnBoardingLayout';
import { useOnBoarding } from '../OnBoardingContext';
import { stepThreeSchema } from "./stepThreeSchema";
import { Field } from "../../../../components/common/Field";

function StepThree() {
  const { formData, updateFormData, nextStep } = useOnBoarding();

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm({
    resolver: zodResolver(stepThreeSchema),
    defaultValues: formData,
  });

  const onSubmit = (data) => {
    updateFormData(data);
    nextStep();
  };

  const getError = (tier, field) => errors.packages?.[tier]?.[field]?.message;

  return (
    <OnboardingLayout 
      title="Set Up Your Service Packages" 
      subtitle="Create clear pricing for your clients."
    >
      <style>{`
        input::-webkit-outer-spin-button,
        input::-webkit-inner-spin-button {
          -webkit-appearance: none;
          margin: 0;
        }
        input[type=number] {
          -moz-appearance: textfield;
        }
      `}</style>

      <form id="onboarding-form" onSubmit={handleSubmit(onSubmit)}>
        <div className="mb-10 flex items-start gap-3 p-4 border border-warning/30 bg-warning-soft rounded-xl">
          <div className="flex-shrink-0 text-warning">⚠️</div>
          <p className="text-xs text-warning-soft-fg leading-relaxed">
            <strong>Feature List:</strong> Please enter each feature on a new line. Each line will automatically appear as a bullet point on your public profile.
          </p>
        </div>

        <div className="grid grid-cols-1 lg:grid-cols-2 gap-8">
          {['starter', 'pro'].map((tier) => (
            <div key={tier} className={`border rounded-2xl p-8 bg-surface shadow-sm transition-all ${getError(tier, 'price') || getError(tier, 'deliveryTime') ? 'border-danger/30 ring-1 ring-red-50' : 'border-line-soft'}`}>
              <div className="mb-8">
                <h2 className="text-xl font-bold text-fg capitalize">{tier}</h2>
                <p className="text-xs text-fg-subtle mt-1">
                  {tier === 'starter' ? 'Perfect for small project tasks' : 'For comprehensive, end-to-end projects'}
                </p>
              </div>

              <div className="space-y-6">
                {/* This whole block is inside a .map() over the pricing tiers,
                    so these three labels were nine unlabelled inputs at
                    runtime, not three. The id from <Field> is spread onto the
                    <input> rather than the wrapper <div> -- the div only exists
                    to position the "$" and "days" affixes, and an id there
                    would associate the label with nothing. */}
                <Field label="Price" error={getError(tier, "price")}>
                  {(f) => (
                    <div className="relative">
                      <span className="absolute left-4 top-1/2 -translate-y-1/2 text-fg-subtle font-medium text-lg" aria-hidden="true">$</span>
                      <input
                        type="number"
                        {...register(`packages.${tier}.price`, { valueAsNumber: true })}
                        placeholder="0"
                        {...f}
                        className="w-full h-[54px] pl-10 pr-4 rounded-xl border border-line focus-visible:border-primary focus-visible:ring-4 focus-visible:ring-primary/10 transition-all outline-none"
                      />
                    </div>
                  )}
                </Field>

                <Field label="Delivery Time" error={getError(tier, "deliveryTime")}>
                  {(f) => (
                    <div className="relative">
                      <input
                        type="number"
                        {...register(`packages.${tier}.deliveryTime`, { valueAsNumber: true })}
                        placeholder="e.g. 5"
                        {...f}
                        className="w-full h-[54px] px-4 rounded-xl border border-line focus-visible:border-primary focus-visible:ring-4 focus-visible:ring-primary/10 transition-all outline-none"
                      />
                      <span className="absolute right-4 top-1/2 -translate-y-1/2 text-xs text-fg-subtle font-bold" aria-hidden="true">days</span>
                    </div>
                  )}
                </Field>

                <Field label="Package Features (One per line)" error={getError(tier, "description")}>
                  {(f) => (
                    <textarea
                      {...register(`packages.${tier}.description`)}
                      placeholder="Example:&#10;Custom Logo Design&#10;3 Revisions&#10;Source Files Included"
                      {...f}
                      className="w-full min-h-[140px] p-5 rounded-xl border border-line focus-visible:border-primary focus-visible:ring-4 focus-visible:ring-primary/10 transition-all outline-none resize-none text-sm leading-relaxed"
                    />
                  )}
                </Field>
              </div>
            </div>
          ))}
        </div>
      </form>
    </OnboardingLayout>
  );
}

export default StepThree;