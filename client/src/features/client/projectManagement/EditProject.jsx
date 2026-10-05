import React, { useEffect, useState } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { CheckCircle2, Lightbulb, X, Plus } from "lucide-react";
import { projectCreateSchema } from "./projectSchemas";
import { useProjectDetails, useAllSkillls, useAllCategories } from "./projectQueries";
import { useUpdateProject } from "./projectMutations";
import { toastError, toastSuccess } from "../../../utils/toast";
import { useFormUnsavedChangesGuard } from "../../../hooks/useUnsavedChangesGuard";
import { Field } from "../../../components/common/Field";

export default function EditProject() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [skillInput, setSkillInput] = useState("");

  const { data: project, isLoading: projectLoading } = useProjectDetails(id);
  const { data: categoriesData } = useAllCategories();
  const { data: skillsResponse } = useAllSkillls();
  
  const { mutateAsync: editProject, isPending: isUpdating } = useUpdateProject();

  const categories = categoriesData ?? [];
  const skillsData = skillsResponse ?? [];

  const { register, handleSubmit, setValue, watch, reset, control, formState: { errors } } = useForm({
    resolver: zodResolver(projectCreateSchema),
  });

  const selectedCategory = watch("category");
  const selectedSkills = watch("skills") || [];
  const pricingType = watch("pricing_type");

  const onFormError = (errors) => {
    console.log("Zod Validation Errors:", errors);
  };

  const suggestedSkills = skillsData
    .filter(
      (skill) =>
        String(skill.category) === String(selectedCategory) &&
        !selectedSkills.includes(skill.name)
    )
    .slice(0, 12);

  useEffect(() => {
    if (project) {
      reset({
        title: project.title,
        description: project.description,
        requirements: project.requirements || "",
        expected_deliverables: project.expected_deliverables || "",
        category: project.category ? String(project.category) : "", 
        pricing_type: project.pricing_type,
        fixed_price: project.fixed_price,
        min_budget: project.min_budget,
        max_budget: project.max_budget,
        delivery_days: project.delivery_days,
        skills: project.skills || [],
      });
    }
  }, [project, reset]);

  // Was an unconditional `beforeunload` listener, so it prompted even with an
  // untouched form -- which trains people to dismiss it reflexively. Now it
  // tracks react-hook-form's `isDirty`, so it is quiet until there is something
  // to lose, and it lifts itself the moment a submit starts.
  useFormUnsavedChangesGuard(control, "This project has unsaved changes.");

  const addSkill = (skillName) => {
    const normalizedSkill = skillName.trim();
    if (!normalizedSkill || selectedSkills.includes(normalizedSkill)) return;
    setValue("skills", [...selectedSkills, normalizedSkill], { shouldValidate: true });
  };

  const handleAddSkill = () => {
    addSkill(skillInput);
    setSkillInput("");
  };

  const onSubmit = async (data) => {
    const formattedData = { ...data };
    // Same DecimalAsStringJsonConverter constraint as AddProject: money must be
    // sent as a JSON string, but these inputs use valueAsNumber. Without this the
    // request 400s with "could not be converted to System.Nullable<Decimal>".
    for (const k of ["fixed_price", "min_budget", "max_budget"]) {
      if (formattedData[k] !== null && formattedData[k] !== undefined) {
        formattedData[k] = String(formattedData[k]);
      }
    }
    if (data.pricing_type === "fixed") {
      formattedData.min_budget = null;
      formattedData.max_budget = null;
    } else {
      formattedData.fixed_price = null;
    }

    try {
      await editProject({ id, data: formattedData });
      navigate("/my-projects");
      toastSuccess("Post Edited Successfully");
    } catch (error) {
      toastError("Update failed");
    }
  };

  if (projectLoading) return <div className="p-10 text-center text-fg-subtle">Loading Project Data...</div>;

  return (
    <div className="min-h-screen bg-surface-2 py-8 px-4 sm:px-6 lg:px-8">
      <div className="max-w-7xl mx-auto">
        <div className="text-center mb-8">
          <h1 className="text-[31px] font-bold text-fg leading-10 mb-2 font-inter">Edit Project</h1>
        </div>

        <div className="grid grid-cols-1 lg:grid-cols-12 gap-8">
          <div className="lg:col-span-8">
            <form onSubmit={handleSubmit(onSubmit, onFormError)} className="bg-surface rounded-xl shadow-sm p-8 border border-line">
              {/* These were bare <label>s sitting *above* their control with no
                  htmlFor and no id on the control, so they named nothing --
                  a screen reader announced the field as just "edit text". The
                  same nine fields in AddProject.jsx already use <Field>, which
                  generates the id and wires label/control/message together. */}
              <div className="mb-8">
                <Field label="Project Title" required error={errors.title?.message}>
                  {(f) => (
                    <input
                      type="text"
                      {...register("title")}
                      {...f}
                      className="w-full px-3 h-[50px] text-base border-line-strong border-2 rounded-lg"
                    />
                  )}
                </Field>
              </div>

              <div className="mb-8">
                <Field label="Project Description" required error={errors.description?.message}>
                  {(f) => (
                    <textarea
                      {...register("description")}
                      {...f}
                      className="w-full px-3 py-4 border-2 min-h-[150px] text-base border-line-strong rounded-lg resize-none"
                    />
                  )}
                </Field>
              </div>

              <div className="mb-8">
                <Field
                  label="Requirements From Freelancer"
                  required
                  error={errors.requirements?.message}
                  hint="What the freelancer needs in order to deliver this."
                >
                  {(f) => (
                    <textarea
                      {...register("requirements")}
                      {...f}
                      className="w-full px-3 py-4 border-2 min-h-[150px] text-base border-line-strong rounded-lg resize-none"
                    />
                  )}
                </Field>
              </div>

              <div className="mb-8">
                <Field label="Expected Deliverables" required error={errors.expected_deliverables?.message}>
                  {(f) => (
                    <textarea
                      {...register("expected_deliverables")}
                      {...f}
                      className="w-full px-3 py-4 border-2 min-h-[150px] text-base border-line-strong rounded-lg resize-none"
                    />
                  )}
                </Field>
              </div>

              <div className="mb-8">
                <Field label="Category" required error={errors.category?.message}>
                  {(f) => (
                    <select
                      {...register("category")}
                      {...f}
                      className="px-3 h-[47px] w-full border-2 border-line-strong rounded-lg bg-surface"
                    >
                      <option value="">Select a category</option>
                      {categories.map((cat) => (
                        <option key={cat.id} value={cat.id}>{cat.name}</option>
                      ))}
                    </select>
                  )}
                </Field>
              </div>

              <div className="mb-8">
                {/* This is a *group*, not a single control: a text box that adds
                    a skill, a list of suggestions, and a set of removable chips.
                    A <label> cannot name a group, so it headed nothing at all.
                    fieldset/legend is the element that can -- and it is the
                    first use in this codebase, which is worth knowing when
                    reading the rest of the forms: a <label> that heads two or
                    more controls wants the same treatment. */}
                <fieldset>
                  <legend className="block text-xs font-medium text-fg-muted mb-2">
                    Required Skills
                    <span className="text-danger-soft-fg ml-1">*</span>
                  </legend>
                  <div className="flex gap-2 mb-4">
                    {/* The visible heading is consumed by the legend, so this
                        control still needs its own name. */}
                    <label htmlFor="add-skill-input" className="sr-only">
                      Add a skill
                    </label>
                    <input
                      id="add-skill-input"
                      type="text"
                      value={skillInput}
                      onChange={(e) => setSkillInput(e.target.value)}
                      onKeyDown={(e) => { if (e.key === "Enter") { e.preventDefault(); handleAddSkill(); }}}
                      placeholder="e.g. React, Python..."
                      className="w-full px-3 border-2 h-[50px] border-line-strong rounded-lg flex-1"
                    />
                    <button type="button" onClick={handleAddSkill} className="px-6 bg-brand text-brand-fg h-[50px] text-sm font-medium rounded-lg hover:bg-brand-hover/80 transition-colors">Add</button>
                  </div>

                {suggestedSkills.length > 0 && (
                  <div className="mb-4">
                    <p className="text-[11px] font-semibold text-fg-subtle uppercase tracking-wider mb-2">Suggested for your category</p>
                    <div className="flex flex-wrap gap-2">
                      {suggestedSkills.map((skill) => (
                          <button
                          key={skill.id}
                          type="button"
                          onClick={() => addSkill(skill.name)}
                          className="flex items-center gap-1.5 px-3 py-1.5 bg-surface border border-line text-fg-muted rounded-full text-xs hover:border-primary hover:text-brand hover:bg-brand-hover-soft transition-all group"
                        >
                          <Plus className="w-3 h-3 text-fg-subtle group-hover:text-brand" />
                          {skill.name}
                        </button>
                      ))}
                    </div>
                  </div>
                )}

                <div className="flex flex-wrap gap-2">
                  {selectedSkills.map((skill, index) => (
                    <span key={index} className="bg-brand text-brand-fg pl-4 pr-2 py-1.5 rounded-full text-sm flex items-center gap-2 shadow-sm animate-in fade-in zoom-in duration-base">
                      {skill}
                      <button
                        type="button"
                        onClick={() => setValue("skills", selectedSkills.filter((_, i) => i !== index))}
                        className="hover:bg-brand-hover rounded-full p-0.5 transition-colors"
                        aria-label={`Remove ${skill}`}
                      >
                        <X className="w-4 h-4" aria-hidden="true" />
                      </button>
                    </span>
                  ))}
                </div>
                {errors.skills && <p className="text-danger-soft-fg text-xs mt-1">{errors.skills.message}</p>}
                </fieldset>
              </div>

              <div className="grid grid-cols-1 md:grid-cols-2 gap-6 mb-8 pt-4 border-t border-line-soft">
                {/* Two radios sharing one name are a group, so the heading is a
                    legend. It was a <label> with neither htmlFor nor a wrapped
                    control, which left the pair announced as two unlabelled
                    radio buttons. The individual radios keep their own
                    wrapping <label>s -- those are correct already. */}
                <fieldset>
                  <legend className="block text-xs font-medium text-fg-muted mb-3">
                    Pricing Type
                    <span className="text-danger-soft-fg ml-1">*</span>
                  </legend>
                  <div className="flex gap-6">
                    <label className="flex items-center gap-2.5 cursor-pointer">
                      <input type="radio" value="fixed" {...register("pricing_type")} className="w-4 h-4 text-brand" />
                      <span className="text-sm text-fg-muted">Fixed Price</span>
                    </label>
                    <label className="flex items-center gap-2.5 cursor-pointer">
                      <input type="radio" value="range" {...register("pricing_type")} className="w-4 h-4 text-brand" />
                      <span className="text-sm text-fg-muted">Range Price</span>
                    </label>
                  </div>
                </fieldset>

                <Field label="Delivery Days" required error={errors.delivery_days?.message}>
                  {(f) => (
                    <input
                      type="number"
                      {...register("delivery_days", { valueAsNumber: true })}
                      {...f}
                      className="w-full px-3 h-[50px] border-2 border-line-strong rounded-lg focus-visible:border-brand outline-none"
                    />
                  )}
                </Field>
              </div>

              {pricingType === "fixed" ? (
                <div className="mb-8 animate-in slide-in-from-top-2 duration-slow">
                  <Field label="Fixed Budget ($)" required error={errors.fixed_price?.message}>
                    {(f) => (
                      <input
                        type="number"
                        {...register("fixed_price", { valueAsNumber: true })}
                        {...f}
                        className="w-full px-3 h-[50px] border-2 border-line-strong rounded-lg focus-visible:border-brand outline-none"
                      />
                    )}
                  </Field>
                </div>
              ) : (
                <div className="grid grid-cols-2 gap-6 mb-8 animate-in slide-in-from-top-2 duration-slow">
                  <Field label="Min Budget ($)" error={errors.min_budget?.message}>
                    {(f) => (
                      <input
                        type="number"
                        placeholder="Min"
                        {...register("min_budget", { valueAsNumber: true })}
                        {...f}
                        className="w-full px-3 h-[50px] border-2 border-line-strong rounded-lg focus-visible:border-primary outline-none"
                      />
                    )}
                  </Field>
                  <Field label="Max Budget ($)" error={errors.max_budget?.message}>
                    {(f) => (
                      <input
                        type="number"
                        placeholder="Max"
                        {...register("max_budget", { valueAsNumber: true })}
                        {...f}
                        className="w-full px-3 h-[50px] border-2 border-line-strong rounded-lg focus-visible:border-primary outline-none"
                      />
                    )}
                  </Field>
                </div>
              )}

              <div className="flex justify-end gap-4 pt-6 border-t border-line-soft">
                <button type="button" onClick={() => navigate(-1)} className="px-6 py-2.5 border border-line-strong rounded-lg text-fg-muted font-medium hover:bg-surface-2 transition-colors">Cancel</button>
                <button type="submit" disabled={isUpdating} className="px-10 py-2.5 bg-brand text-brand-fg font-bold rounded-lg hover:bg-brand-hover disabled:opacity-50 shadow-lg shadow-blue-100 transition-all">
                  {isUpdating ? "Saving Changes..." : "Save Changes"}
                </button>
              </div>
            </form>
          </div>

          <div className="lg:col-span-4">
             <div className="bg-surface border border-brand-line rounded-xl p-6 sticky top-8 shadow-sm">
                <div className="flex items-center gap-3 mb-6">
                  <div className="p-2 bg-brand-soft rounded-lg">
                    <Lightbulb className="w-5 h-5 text-brand" />
                  </div>
                  <h3 className="text-lg font-bold text-fg">Editing Guide</h3>
                </div>
                <div className="space-y-5">
                  <div className="flex items-start gap-3">
                    <CheckCircle2 className="w-5 h-5 text-success-soft-fg shrink-0 mt-0.5" />
                    <p className="text-sm text-fg-muted leading-relaxed">Changing the category refreshes the <b>Suggested Skills</b> to help you find specialized talent.</p>
                  </div>
                  <div className="flex items-start gap-3">
                    <CheckCircle2 className="w-5 h-5 text-success-soft-fg shrink-0 mt-0.5" />
                    <p className="text-sm text-fg-muted leading-relaxed">Detailed requirements attract higher quality proposals from experts.</p>
                  </div>
                  <div className="flex items-start gap-3">
                    <CheckCircle2 className="w-5 h-5 text-success-soft-fg shrink-0 mt-0.5" />
                    <p className="text-sm text-fg-muted leading-relaxed">Ensure your budget matches the complexity of the project for better results.</p>
                  </div>
                </div>
             </div>
          </div>
        </div>
      </div>
    </div>
  );
}
