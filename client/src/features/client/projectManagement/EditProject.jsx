import { zodResolver } from "@hookform/resolvers/zod";
import { useEffect } from "react";
import { useForm } from "react-hook-form";
import toast from "react-hot-toast";
import { useNavigate, useParams } from "react-router-dom";
import { useCategories, useProjectDetails, useSkills } from "./projectQueries.js";
import { useUpdateProject } from "./projectMutations.js";
import { projectCreateSchema } from "./projectSchemas.js";

export default function EditProject() {
  const { id } = useParams();
  const navigate = useNavigate();
  const mutation = useUpdateProject();
  const { data: projectData, isLoading } = useProjectDetails(id);
  const { data: categoryData } = useCategories();
  const categories = categoryData?.results || categoryData || [];
  const { register, handleSubmit, watch, setValue, reset, formState: { errors } } = useForm({
    resolver: zodResolver(projectCreateSchema),
    defaultValues: {
      title: "",
      description: "",
      requirements: "",
      expectedDeliverables: "",
      pricingType: 1,
      fixedPrice: "",
      minPrice: "",
      maxPrice: "",
      expectedDays: "",
      categoryId: "",
      skillIds: [],
      customSkillNames: [],
    },
  });

  useEffect(() => {
    if (!projectData) return;
    reset({
      title: projectData.title || "",
      description: projectData.description || "",
      requirements: projectData.requirements || "",
      expectedDeliverables: projectData.expectedDeliverables || "",
      pricingType: projectData.pricingType === "Range" ? 2 : 1,
      fixedPrice: projectData.fixedPrice ?? "",
      minPrice: projectData.minPrice ?? "",
      maxPrice: projectData.maxPrice ?? "",
      expectedDays: projectData.expectedDays ?? "",
      categoryId: projectData.categoryId ?? "",
      skillIds: (projectData.skills || []).map((skill) => skill.skillId ?? skill.id).filter(Boolean),
      customSkillNames: [],
    });
  }, [projectData, reset]);

  const categoryId = watch("categoryId");
  const { data: skillData } = useSkills({ categoryId: categoryId ? Number(categoryId) : undefined });
  const skillOptions = skillData?.results || skillData || [];
  const pricingType = Number(watch("pricingType") || 1);

  const toggleSkill = (skillId) => {
    const current = watch("skillIds") || [];
    const next = current.includes(skillId) ? current.filter((id) => id !== skillId) : [...current, skillId];
    setValue("skillIds", next, { shouldDirty: true, shouldValidate: true });
  };

  const onSubmit = (values) => {
    const payload = {
      title: values.title,
      description: values.description,
      requirements: values.requirements,
      expectedDeliverables: values.expectedDeliverables,
      expectedDays: Number(values.expectedDays),
      categoryId: values.categoryId ? Number(values.categoryId) : null,
      skillIds: (values.skillIds || []).map(Number),
      customSkillNames: values.customSkillNames || [],
    };

    mutation.mutate(
      { id: Number(id), data: payload },
      {
        onSuccess: () => {
          toast.success("Project updated.");
          navigate(`/my-projects/${id}`);
        },
        onError: (error) => {
          const message = error?.response?.data?.detail || error?.response?.data?.message || "Unable to update project.";
          toast.error(message);
        },
      }
    );
  };

  if (isLoading) {
    return <div className="p-8 text-center text-slate-500">Loading project…</div>;
  }

  return (
    <div className="mx-auto max-w-4xl px-4 py-8 sm:px-6 lg:px-8">
      <div className="mb-8">
        <p className="text-sm font-semibold uppercase tracking-[0.22em] text-blue-600">Marketplace</p>
        <h1 className="mt-2 text-3xl font-bold text-slate-900">Edit project</h1>
      </div>

      <form onSubmit={handleSubmit(onSubmit)} className="space-y-6 rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
        <div className="grid gap-6 md:grid-cols-2">
          <div className="md:col-span-2">
            <label className="mb-2 block text-sm font-medium text-slate-700">Project title</label>
            <input {...register("title")} className="w-full rounded-xl border border-slate-200 px-3 py-2.5 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100" />
            {errors.title && <p className="mt-2 text-sm text-red-600">{errors.title.message}</p>}
          </div>

          <div className="md:col-span-2">
            <label className="mb-2 block text-sm font-medium text-slate-700">Description</label>
            <textarea {...register("description")} rows={4} className="w-full rounded-xl border border-slate-200 px-3 py-2.5 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100" />
            {errors.description && <p className="mt-2 text-sm text-red-600">{errors.description.message}</p>}
          </div>

          <div className="md:col-span-2">
            <label className="mb-2 block text-sm font-medium text-slate-700">Requirements</label>
            <textarea {...register("requirements")} rows={3} className="w-full rounded-xl border border-slate-200 px-3 py-2.5 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100" />
            {errors.requirements && <p className="mt-2 text-sm text-red-600">{errors.requirements.message}</p>}
          </div>

          <div className="md:col-span-2">
            <label className="mb-2 block text-sm font-medium text-slate-700">Expected deliverables</label>
            <textarea {...register("expectedDeliverables")} rows={3} className="w-full rounded-xl border border-slate-200 px-3 py-2.5 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100" />
            {errors.expectedDeliverables && <p className="mt-2 text-sm text-red-600">{errors.expectedDeliverables.message}</p>}
          </div>

          <div>
            <label className="mb-2 block text-sm font-medium text-slate-700">Expected days</label>
            <input type="number" min="1" {...register("expectedDays")} className="w-full rounded-xl border border-slate-200 px-3 py-2.5 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100" />
            {errors.expectedDays && <p className="mt-2 text-sm text-red-600">{errors.expectedDays.message}</p>}
          </div>

          <div>
            <label className="mb-2 block text-sm font-medium text-slate-700">Category</label>
            <select {...register("categoryId")} className="w-full rounded-xl border border-slate-200 bg-white px-3 py-2.5 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100">
              <option value="">Select a category</option>
              {(categories || []).map((category) => (
                <option key={category.id ?? category.Id} value={category.id ?? category.Id}>
                  {category.name ?? category.Name}
                </option>
              ))}
            </select>
          </div>

          <div className="md:col-span-2">
            <label className="mb-2 block text-sm font-medium text-slate-700">Skills</label>
            <div className="flex flex-wrap gap-2 rounded-xl border border-slate-200 p-3">
              {(skillOptions || []).map((skill) => {
                const skillId = skill.id ?? skill.Id;
                const selected = (watch("skillIds") || []).includes(skillId);
                return (
                  <button
                    key={skillId}
                    type="button"
                    onClick={() => toggleSkill(skillId)}
                    className={`rounded-full border px-3 py-1.5 text-xs font-medium transition ${selected ? "border-blue-600 bg-blue-50 text-blue-700" : "border-slate-200 bg-white text-slate-600 hover:border-slate-300"}`}
                  >
                    {skill.name ?? skill.Name}
                  </button>
                );
              })}
            </div>
          </div>

          <div className="md:col-span-2">
            <div className="rounded-xl border border-slate-200 bg-slate-50 p-4 text-sm text-slate-600">
              Pricing type stays as configured on the original project; edit the price, budget, or range via the original project creation flow if needed.
            </div>
          </div>
        </div>

        <div className="flex items-center justify-end gap-3 pt-2">
          <button type="button" onClick={() => navigate(`/my-projects/${id}`)} className="rounded-xl border border-slate-200 px-4 py-2.5 text-sm font-medium text-slate-700 hover:bg-slate-50">Cancel</button>
          <button type="submit" disabled={mutation.isPending} className="rounded-xl bg-blue-600 px-4 py-2.5 text-sm font-semibold text-white hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-60">
            {mutation.isPending ? "Saving..." : "Save changes"}
          </button>
        </div>
      </form>
    </div>
  );
}
