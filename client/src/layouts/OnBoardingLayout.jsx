import { Outlet } from "react-router-dom";
import { useOnboarding } from "../features/client/onboarding/OnBoardingContext.jsx";

export default function OnBoardingLayout({ children, title = "Freelancer onboarding", subtitle = "Set up the essentials for your WebOfWork profile." }) {
  const { values } = useOnboarding();
  const currentStep = Number(values.currentStep ?? 1);
  const totalSteps = 6;

  return (
    <div className="onboarding-shell">
      <div className="onboarding-shell__header">
        <span className="app-shell__eyebrow">Profile setup</span>
        <h1>{title}</h1>
        <p>{subtitle}</p>
      </div>
      <div className="onboarding-shell__progress" aria-label={`Step ${currentStep} of ${totalSteps}`}>
        {Array.from({ length: totalSteps }, (_, index) => <span className={index < currentStep ? "onboarding-shell__step onboarding-shell__step--active" : "onboarding-shell__step"} key={index} />)}
      </div>
      <div className="onboarding-shell__content">{children ?? <Outlet />}</div>
    </div>
  );
}