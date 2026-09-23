import { createContext, useContext, useState } from "react";

const OnboardingContext = createContext(null);

export function OnboardingProvider({ children, initialValue = {} }) {
  const [values, setValues] = useState(initialValue);
  const updateValues = (next) => setValues((current) => ({ ...current, ...next }));
  return <OnboardingContext.Provider value={{ values, updateValues, setValues }}>{children}</OnboardingContext.Provider>;
}

export const useOnboarding = () => {
  const context = useContext(OnboardingContext);
  if (!context) throw new Error("useOnboarding must be used inside OnboardingProvider");
  return context;
};
