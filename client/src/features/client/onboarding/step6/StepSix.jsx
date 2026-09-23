import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { CreditCard } from "lucide-react";
import { stepSixSchema } from "./stepSixSchema.js";

export default function StepSix({ values, onBack, onSubmitFinal, isSubmitting }) {
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm({
    resolver: zodResolver(stepSixSchema),
    defaultValues: {
      paypalEmail: values.paypalEmail ?? "",
    },
  });

  const onSubmit = async (data) => {
    await onSubmitFinal({ ...data });
  };

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="mx-auto max-w-2xl space-y-6">
      <div className="space-y-2">
        <p className="text-xs font-semibold uppercase tracking-[0.2em] text-blue-600">Step 6</p>
        <h2 className="text-3xl font-bold text-slate-900">Add payout details</h2>
        <p className="text-sm text-slate-600">This is where your earnings are sent after client approval.</p>
      </div>

      <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
        <div className="mb-4 flex items-center gap-3 rounded-xl bg-blue-50 p-3 text-blue-700">
          <CreditCard className="h-5 w-5" />
          <span className="text-sm font-medium">PayPal email</span>
        </div>

        <label className="mb-1 block text-sm font-medium text-slate-700">PayPal account email</label>
        <input
          {...register("paypalEmail")}
          type="email"
          placeholder="you@example.com"
          className="w-full rounded-xl border border-slate-200 bg-white px-3 py-2.5 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100"
        />
        {errors.paypalEmail && <p className="mt-1 text-xs text-red-500">{errors.paypalEmail.message}</p>}
      </div>

      <div className="flex items-center justify-between pt-2">
        <button type="button" onClick={onBack} className="rounded-xl border border-slate-200 bg-white px-4 py-2.5 text-sm font-medium text-slate-600 hover:bg-slate-100">
          Back
        </button>
        <button type="submit" disabled={isSubmitting} className="rounded-xl bg-blue-600 px-5 py-2.5 text-sm font-semibold text-white hover:bg-blue-700 disabled:cursor-not-allowed disabled:bg-slate-300">
          {isSubmitting ? "Submitting..." : "Finish onboarding"}
        </button>
      </div>
    </form>
  );
}
