export const buildOnboardingPayload = (steps) => ({
  tagline: steps.tagline ?? "",
  bio: steps.bio ?? "",
  phone: steps.phone ?? "",
  experienceLevel: steps.experienceLevel ?? "",
  primaryCategory: steps.primaryCategory ?? steps.categoryId ?? null,
  skills: (steps.skills ?? []).map((skill) => typeof skill === "number" ? { skillId: skill, customName: "" } : {
    skillId: skill.skillId ?? null,
    customName: skill.customName ?? "",
  }),
  packages: (steps.packages ?? []).map((item) => ({
    packageType: item.packageType ?? item.title ?? "",
    price: Number(item.price ?? 0),
    deliveryDays: Number(item.deliveryDays ?? 0),
    description: item.description ?? "",
  })),
});

export const mergeOnboardingStep = (current, step) => ({ ...current, ...step });
