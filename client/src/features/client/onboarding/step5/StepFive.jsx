import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { CheckCircle2, ClipboardCheck } from "lucide-react";
import { stepFiveSchema } from "./stepFiveSchema.js";

export default function StepFive({ values, onNext, onBack }) {
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm({
    resolver: zodResolver(stepFiveSchema),
    defaultValues: { confirmed: false },
  });

  const packages = Array.isArray(values.packages) ? values.packages : [];
  const portfolio = Array.isArray(values.portfolio) ? values.portfolio : [];

  const onSubmit = (data) => {
    if (data.confirmed) {
      onNext({ confirmed: true });
    }
  };

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="mx-auto max-w-3xl space-y-6">
      <div className="space-y-2">
        <p className="text-xs font-semibold uppercase tracking-[0.2em] text-blue-600">Step 5</p>
        <h2 className="text-3xl font-bold text-slate-900">Review your profile setup</h2>
      </div>

      <div className="space-y-4 rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
        <div className="flex items-center gap-3 rounded-xl bg-slate-50 p-3">
          <ClipboardCheck className="h-5 w-5 text-blue-600" />
          <div>
            <p className="text-sm font-semibold text-slate-800">Professional overview</p>
            <p className="text-xs text-slate-500">{values.tagline || "No tagline set"}</p>
          </div>
        </div>

        <div className="grid gap-4 md:grid-cols-2">
          <div className="rounded-xl border border-slate-200 bg-slate-50 p-4">
            <p className="text-xs font-semibold uppercase tracking-[0.15em] text-slate-500">Skills</p>
            <div className="mt-3 flex flex-wrap gap-2">
              {Array.isArray(values.skills) && values.skills.length > 0 ? (
                values.skills.map((skill, index) => (
                  <span key={`${skill.skillId || skill.id || "custom"}-${index}`} className="rounded-full bg-blue-50 px-2.5 py-1 text-xs font-medium text-blue-700">
                    {skill.customName || skill.name || skill.skillId}
                  </span>
                ))
              ) : (
                <span className="text-xs text-slate-400">No skills selected yet.</span>
              )}
            </div>
          </div>

          <div className="rounded-xl border border-slate-200 bg-slate-50 p-4">
            <p className="text-xs font-semibold uppercase tracking-[0.15em] text-slate-500">Packages</p>
            <div className="mt-3 space-y-1 text-sm text-slate-700">
              {packages.length > 0 ? (
                packages.map((pkg, index) => (
                  <div key={`${pkg.packageType}-${index}`} className="flex items-center justify-between gap-3">
                    <span>{pkg.packageType}</span>
                    <span className="font-medium">${pkg.price}</span>
                  </div>
                ))
              ) : (
                <span className="text-xs text-slate-400">No packages added yet.</span>
              )}
            </div>
          </div>
        </div>

        <div className="rounded-xl border border-slate-200 bg-slate-50 p-4">
          <p className="text-xs font-semibold uppercase tracking-[0.15em] text-slate-500">Portfolio samples</p>
          <p className="mt-2 text-sm text-slate-700">{portfolio.length} items ready to publish</p>
        </div>

        <label className="flex items-start gap-3 rounded-xl border border-slate-200 bg-slate-50 p-4 text-sm text-slate-700">
          <input type="checkbox" {...register("confirmed")} className="mt-1 h-4 w-4 accent-blue-600" />
          <span>
            I confirm this profile summary is ready to publish and my contact details are accurate.
          </span>
        </label>
        {errors.confirmed && <p className="text-xs text-red-500">{errors.confirmed.message}</p>}
      </div>

      <div className="flex items-center justify-between pt-2">
        <button type="button" onClick={onBack} className="rounded-xl border border-slate-200 bg-white px-4 py-2.5 text-sm font-medium text-slate-600 hover:bg-slate-100">
          Back
        </button>
        <button type="submit" className="inline-flex items-center gap-2 rounded-xl bg-blue-600 px-5 py-2.5 text-sm font-semibold text-white hover:bg-blue-700">
          <CheckCircle2 className="h-4 w-4" /> Continue
        </button>
      </div>
    </form>
  );
}
