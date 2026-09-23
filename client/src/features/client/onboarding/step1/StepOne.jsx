import { useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { CheckCircle2, Info, Phone } from "lucide-react";
import PhoneOtpModal from "./PhoneOtpModal.jsx";
import { stepOneSchema } from "./stepOneSchema.js";

export default function StepOne({ values, onNext, onBack }) {
  const [isPhoneModalOpen, setIsPhoneModalOpen] = useState(false);
  const {
    register,
    handleSubmit,
    getValues,
    formState: { errors },
  } = useForm({
    resolver: zodResolver(stepOneSchema),
    defaultValues: {
      tagline: values.tagline ?? "",
      bio: values.bio ?? "",
      phone: values.phone ?? "",
      experienceLevel: values.experienceLevel ?? "beginner",
    },
  });

  const phone = getValues("phone") || "";

  const onSubmit = (data) => {
    onNext({ ...data });
  };

  return (
    <>
      <form onSubmit={handleSubmit(onSubmit)} className="mx-auto max-w-3xl space-y-6">
        <div className="space-y-2">
          <p className="text-xs font-semibold uppercase tracking-[0.2em] text-blue-600">Step 1</p>
          <h2 className="text-3xl font-bold text-slate-900">Tell clients who you are</h2>
          <p className="text-sm text-slate-600">Add your headline, location of expertise, and contact details so clients can trust your work.</p>
        </div>

        <div className="space-y-4 rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
          <div>
            <label className="mb-1 block text-sm font-medium text-slate-700">Professional tagline</label>
            <input
              {...register("tagline")}
              className="w-full rounded-xl border border-slate-200 px-3 py-2.5 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100"
              placeholder="I design clean mobile-first product experiences for founders."
            />
            {errors.tagline && <p className="mt-1 text-xs text-red-500">{errors.tagline.message}</p>}
          </div>

          <div>
            <label className="mb-1 block text-sm font-medium text-slate-700">Bio</label>
            <textarea
              {...register("bio")}
              rows={5}
              className="w-full rounded-xl border border-slate-200 px-3 py-2.5 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100"
              placeholder="I help founders launch polished products, landing pages, and user-friendly workflows with strong attention to detail."
            />
            {errors.bio && <p className="mt-1 text-xs text-red-500">{errors.bio.message}</p>}
          </div>

          <div>
            <label className="mb-1 block text-sm font-medium text-slate-700">Phone number</label>
            <div className="flex gap-3">
              <div className="relative flex-1">
                <Phone className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
                <input
                  {...register("phone")}
                  className="w-full rounded-xl border border-slate-200 bg-white pl-10 pr-3 py-2.5 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100"
                  placeholder="+94 77 123 4567"
                />
              </div>
              <button type="button" onClick={() => setIsPhoneModalOpen(true)} className="rounded-xl border border-blue-200 bg-blue-50 px-3 py-2.5 text-sm font-medium text-blue-700 hover:bg-blue-100">
                Verify
              </button>
            </div>
            {errors.phone && <p className="mt-1 text-xs text-red-500">{errors.phone.message}</p>}
          </div>

          <div>
            <label className="mb-1 block text-sm font-medium text-slate-700">Experience level</label>
            <select
              {...register("experienceLevel")}
              className="w-full rounded-xl border border-slate-200 bg-white px-3 py-2.5 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100"
            >
              <option value="beginner">Beginner</option>
              <option value="intermediate">Intermediate</option>
              <option value="expert">Expert</option>
            </select>
            {errors.experienceLevel && <p className="mt-1 text-xs text-red-500">{errors.experienceLevel.message}</p>}
          </div>
        </div>

        <div className="flex items-center justify-between gap-4 rounded-2xl border border-slate-200 bg-slate-50 p-4 text-sm text-slate-600">
          <div className="flex items-center gap-2 text-amber-700">
            <Info className="h-4 w-4" />
            Keep your profile clear and client-focused.
          </div>
          <div className="inline-flex items-center gap-2 rounded-full bg-emerald-50 px-3 py-1 text-xs font-medium text-emerald-700">
            <CheckCircle2 className="h-3.5 w-3.5" />
            {phone ? "Phone ready" : "Phone not verified yet"}
          </div>
        </div>

        <div className="flex items-center justify-between pt-2">
          <button type="button" onClick={onBack} className="rounded-xl border border-slate-200 bg-white px-4 py-2.5 text-sm font-medium text-slate-600 hover:bg-slate-100">
            Back
          </button>
          <button type="submit" className="rounded-xl bg-blue-600 px-5 py-2.5 text-sm font-semibold text-white hover:bg-blue-700">
            Continue
          </button>
        </div>
      </form>

      <PhoneOtpModal isOpen={isPhoneModalOpen} phone={phone} onClose={() => setIsPhoneModalOpen(false)} onVerified={() => {}} />
    </>
  );
}
