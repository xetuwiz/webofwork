import { useNavigate } from "react-router-dom";
import toast from "react-hot-toast";
import { useOnboarding } from "./OnBoardingContext.jsx";
import { buildOnboardingPayload } from "./onBoardingHelper.js";
import { useCreatePortfolioItem, useSubmitFreelancerOnboarding } from "./onBoardingMutations.js";
import StepOne from "./step1/StepOne.jsx";
import StepTwo from "./step2/StepTwo.jsx";
import StepThree from "./step3/StepThree.jsx";
import StepFour from "./step4/StepFour.jsx";
import StepFive from "./step5/StepFive.jsx";
import StepSix from "./step6/StepSix.jsx";

const steps = [StepOne, StepTwo, StepThree, StepFour, StepFive, StepSix];

export default function OnboardingManager() {
  const navigate = useNavigate();
  const { values, setValues } = useOnboarding();
  const currentStep = Number(values?.currentStep ?? 1);
  const { mutateAsync: submitOnboarding, isPending: isSubmitting } = useSubmitFreelancerOnboarding();
  const { mutateAsync: createPortfolioItem } = useCreatePortfolioItem();

  const updateStep = (stepPayload) => {
    setValues((current) => ({
      ...current,
      ...stepPayload,
      currentStep: currentStep + 1,
    }));
  };

  const goBack = () => {
    setValues((current) => ({
      ...current,
      currentStep: Math.max(1, currentStep - 1),
    }));
  };

  const handleFinalSubmit = async (stepPayload) => {
    try {
      const payload = buildOnboardingPayload({ ...values, ...stepPayload });
      await submitOnboarding(payload);

      const portfolioItems = Array.isArray(values.portfolio) ? values.portfolio : [];
      if (portfolioItems.length > 0) {
        await Promise.all(
          portfolioItems.map((item) =>
            createPortfolioItem({
              title: item.title,
              description: item.description,
              imageUrl: item.imageUrl,
            }),
          ),
        );
      }

      toast.success("Freelancer onboarding complete.");
      navigate("/freelancer/profile");
    } catch (error) {
      toast.error(error?.response?.data?.detail || "Unable to complete onboarding.");
    }
  };

  const CurrentStep = steps[currentStep - 1] ?? StepOne;

  return (
    <CurrentStep
      values={values}
      onNext={updateStep}
      onBack={goBack}
      onSubmitFinal={handleFinalSubmit}
      isSubmitting={isSubmitting}
    />
  );
}
