import React from "react";
import { useOnBoarding } from "../features/client/onboarding/OnBoardingContext";

export default function OnboardingLayout({ children, title, subtitle }) {
  const { currentStep, totalSteps, prevStep } = useOnBoarding();

  return (
    <div className="min-h-screen flex items-center justify-center bg-surface-2 px-4 py-10">
      <div className="w-full max-w-page-lg">
        <div className="text-center pb-10">
          {/* The gradient ran from `black` to the brand colour, which is invisible
              in dark mode -- both ends have to come from the token set. */}
          <h1 className="text-5xl font-black tracking-tighter bg-linear-to-r from-fg to-brand bg-clip-text text-transparent">
            FREELANCER ONBOARDING
          </h1>
          <div className="h-1 w-20 bg-brand mx-auto mt-4 rounded-full" />
        </div>

        <div className="flex flex-col items-center gap-3 mb-12">
          <div className="text-xs font-medium text-fg-muted">
            Step {currentStep} of {totalSteps}
          </div>
          <div className="flex gap-[7px] w-full max-w-[400px]">
            {Array.from({ length: totalSteps }).map((_, index) => (
              <div
                key={index}
                className={`h-2 rounded-full flex-1 transition-all duration-slow ${
                  index < currentStep ? "bg-brand" : "bg-surface-inset"
                }`}
              />
            ))}
          </div>
        </div>

        <div className="text-center mb-12">
          <h1 className="text-[32px] font-extrabold text-fg leading-tight mb-3">
            {title}
          </h1>
          <p className="text-[16px] text-fg-muted max-w-lg mx-auto leading-relaxed">
            {subtitle}
          </p>
        </div>

        <div className="border border-line rounded-2xl bg-surface shadow-xl p-8 sm:p-14">
          {children}
        </div>

        <div className="flex justify-between items-center mt-12 gap-4">
          <button
            type="button"
            onClick={prevStep}
            disabled={currentStep === 1}
            className="h-12 px-8 rounded-xl bg-surface border border-line text-fg-muted text-sm font-bold hover:bg-surface-3 disabled:opacity-30 transition-all"
          >
            Back
          </button>

          <button
            type="submit"
            form="onboarding-form"
            className="h-12 px-10 rounded-xl bg-brand hover:bg-brand-hover text-brand-fg text-sm font-bold shadow-lg shadow-brand/20 transition-all"
          >
            {currentStep === totalSteps ? 'Complete Profile' : 'Next Step'}
          </button>
        </div>
      </div>
    </div>
  );
}
