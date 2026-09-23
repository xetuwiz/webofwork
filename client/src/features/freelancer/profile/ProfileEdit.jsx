import { useForm } from "react-hook-form";
import { Loader2 } from "lucide-react";
import { useFreelancerProfile } from "./profileQueries.js";
import { useUpdateFreelancerProfile } from "./profileMutation.js";

export default function ProfileEdit() {
  const { data: profile, isLoading } = useFreelancerProfile();
  const { mutateAsync: updateProfile, isPending } = useUpdateFreelancerProfile();

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm({
    defaultValues: {
      fullName: profile?.full_name || "",
      tagline: profile?.tagline || "",
      bio: profile?.bio || "",
      experienceLevel: profile?.experience_level || profile?.experienceLevel || "beginner",
      availability: Boolean(profile?.availability),
    },
  });

  const onSubmit = async (payload) => {
    await updateProfile({
      fullName: payload.fullName,
      tagline: payload.tagline,
      bio: payload.bio,
      experienceLevel: payload.experienceLevel,
      availability: payload.availability,
    });
  };

  if (isLoading) return <div className="p-8 text-center text-sm text-slate-500">Loading profile...</div>;

  return (
    <div className="mx-auto max-w-3xl px-4 py-10">
      <h1 className="mb-6 text-3xl font-bold text-slate-900">Edit profile</h1>

      <form onSubmit={handleSubmit(onSubmit)} className="space-y-6 rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
        <div>
          <label className="mb-1 block text-sm font-medium text-slate-700">Full name</label>
          <input {...register("fullName")} className="w-full rounded-xl border border-slate-200 px-3 py-2.5 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100" />
          {errors.fullName && <p className="mt-1 text-xs text-red-500">{errors.fullName.message}</p>}
        </div>

        <div>
          <label className="mb-1 block text-sm font-medium text-slate-700">Tagline</label>
          <input {...register("tagline")} className="w-full rounded-xl border border-slate-200 px-3 py-2.5 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100" />
        </div>

        <div>
          <label className="mb-1 block text-sm font-medium text-slate-700">Bio</label>
          <textarea {...register("bio")} rows={5} className="w-full rounded-xl border border-slate-200 px-3 py-2.5 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100" />
        </div>

        <div>
          <label className="mb-1 block text-sm font-medium text-slate-700">Experience level</label>
          <select {...register("experienceLevel")} className="w-full rounded-xl border border-slate-200 bg-white px-3 py-2.5 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100">
            <option value="beginner">Beginner</option>
            <option value="intermediate">Intermediate</option>
            <option value="expert">Expert</option>
          </select>
        </div>

        <label className="flex items-center gap-3 rounded-xl border border-slate-200 bg-slate-50 p-3 text-sm text-slate-700">
          <input type="checkbox" {...register("availability")} className="h-4 w-4 accent-blue-600" />
          Available for new work
        </label>

        <button type="submit" disabled={isPending} className="inline-flex items-center gap-2 rounded-xl bg-blue-600 px-5 py-2.5 text-sm font-semibold text-white hover:bg-blue-700 disabled:cursor-not-allowed disabled:bg-slate-300">
          {isPending && <Loader2 className="h-4 w-4 animate-spin" />} Save profile
        </button>
      </form>
    </div>
  );
}
