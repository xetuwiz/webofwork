/**
 * Fixtures for the label-association render check.
 *
 * Aliased over the real data hooks by `ssr-check/vite.ssr.config.mjs`. The
 * point is not to test the queries -- it is to get the form to render, so the
 * emitted HTML can be inspected for label/control association.
 */
import { useState } from "react";

const profile = {
  id: "p1",
  fullName: "Ada Lovelace",
  tagline: "Analytical engine whisperer",
  bio: "Builds things.",
  categoryId: "7",
  experienceLevel: "expert",
  isAvailable: true,
  hourlyRate: 80,
  packages: {
    basic: { price: "250", deliveryTime: "3", description: "Starter" },
    standard: { price: "800", deliveryTime: "10", description: "Full build" },
    premium: { price: "2000", deliveryTime: "21", description: "Long haul" },
  },
  skills: [{ id: 1, name: "React" }],
  portfolio: [],
  profile_photo: null,
};

/** Mirrors the shape the component destructures, without touching the network. */
export function useFreelancerProfile() {
  return { data: profile, isLoading: false, isError: false };
}

export function useUpdateFreelancerProfile() {
  const [isPending, setIsPending] = useState(false);
  return {
    mutateAsync: async () => {
      setIsPending(true);
      setIsPending(false);
      return profile;
    },
    isPending,
  };
}

/**
 * A plain array, not a paged envelope.
 *
 * The two consumers disagree about the shape on purpose: EditProject does
 * `categoriesData ?? []` and calls `.map` straight on it, while ProfileEdit
 * does `Array.isArray(categoriesData) ? categoriesData : categoriesData?.results || []`.
 * A paged fixture crashed EditProject with "categories.map is not a function";
 * an array satisfies both. The envelope the real endpoints return is a
 * separate concern and is not what this harness is testing.
 */
export function useAllCategories() {
  return { data: [{ id: 7, name: "Engineering" }, { id: 9, name: "Design" }] };
}

export function useAllSkillls() {
  return {
    data: [
      { id: 1, name: "React", category: 7 },
      { id: 2, name: "Python", category: 7 },
    ],
  };
}

export function useProjectDetails() {
  return {
    data: {
      id: 1,
      title: "Existing project",
      description: "d",
      requirements: "r",
      expected_deliverables: "e",
      category: 7,
      skills: ["React"],
      pricing_type: "fixed",
      fixed_price: 500,
      delivery_days: 7,
    },
    isLoading: false,
  };
}

export function useUpdateProject() {
  return { mutateAsync: async () => ({}), isPending: false };
}

export function useCreateProject() {
  return { mutateAsync: async () => ({}), isPending: false };
}

/* ---- onboarding wizard -------------------------------------------------- */

const wizard = {
  step: 1,
  nextStep: () => {},
  prevStep: () => {},
  goToStep: () => {},
  formData: {},
  setFormData: () => {},
  isSubmitting: false,
  updateFormData: () => {},
};

export function useOnBoarding() {
  return wizard;
}

export function useSendPhoneOtp() {
  return { mutateAsync: async () => ({}), isPending: false };
}

export function useSubmitOnboarding() {
  return { mutateAsync: async () => ({}), isPending: false };
}

export function useModal() {
  return { openModal: () => {}, closeModal: () => {}, modal: null, isOpen: false };
}

export async function uploadToCloudinary() {
  return { secure_url: "https://example.invalid/preview.png" };
}

export default {};
