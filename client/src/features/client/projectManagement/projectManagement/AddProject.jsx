import { CheckCircle2, Lightbulb } from 'lucide-react';
import { useState } from 'react'
import { useForm } from 'react-hook-form';
import { zodResolver } from "@hookform/resolvers/zod";
import { projectCreateSchema } from "./projectSchemas";
// Only `useAllSkillls`. The other two were imported and never used -- this page
// reads categories through the admin module's `useAllCategories` (see line 13),
// so `useCategories` was always going to be dead here. Kept as a note rather than
// silently deleted, because a client page reaching into `admin/` for its own
// category list is a layering smell worth revisiting, not a one-line fix.
import { useAllSkillls } from './projectQueries';
import { selectDraftSkills } from './draftSkills';
import { useCreateProject } from "./projectMutations";
import { useModal } from '../../../hooks/modal/useModalStore';
import { useNavigate } from 'react-router-dom';
import { Field } from '../../../components/common/Field';
import { useFormUnsavedChangesGuard } from '../../../hooks/useUnsavedChangesGuard';
import { useAllCategories } from '../../admin/categorymanagement/categoryQueries';
import { toastError, toastSuccess } from "../../../utils/toast";
import DraftAssistButton from "../../aiworkflow/DraftAssistButton";
import AiFieldBadge, { AiFieldEdited } from "../../aiworkflow/AiFieldBadge";

function AddProject() {
  const [skillInput, setSkillInput] = useState("");

  // Which fields the AI filled, and which the client has since changed.
  // A Set rather than a boolean per field because the fields are named
  // dynamically by `applyDraft`, and a fixed list of booleans would need
  // editing every time the draft learned to fill a new one.
  const [aiFilled, setAiFilled] = useState(() => new Set());

  const navigate = useNavigate();
  const { openModal, closeModal } = useModal();

  // `getValues` is needed by applyDraft: the draft fills only what is still
  // empty, and that requires reading the current form without re-rendering.
  // `getFieldState` drives the AI badge: dirty means the client typed, because
  // applyDraft fills with `shouldDirty: false`.
  const { register, handleSubmit, setValue, getValues, getFieldState, watch, control, formState: { errors }, } = useForm({
    resolver: zodResolver(projectCreateSchema),
    shouldUnregister: true,
    defaultValues: {
      pricing_type: "fixed",
      skills: [],
      delivery_days: 0,
    }
  });

  const selectedCategory = watch("category");
  const selectedSkills = watch("skills") || [];
  const pricingType = watch("pricing_type");

  const { data: categoriesData } = useAllCategories();
  const categories = categoriesData ?? [];
  const { data: skillsResponse } = useAllSkillls();
  const skillsData = skillsResponse ?? [];
  const { mutateAsync: createProject, isPending } = useCreateProject();

  // Was an unconditional `beforeunload` listener, so it prompted even with an
  // untouched form -- which trains people to dismiss it reflexively. Now it
  // tracks react-hook-form's `isDirty`, so it is quiet until there is something
  // to lose, and it lifts itself the moment a submit starts.
  useFormUnsavedChangesGuard(control, "This project has unsaved changes.");

  // There used to be an effect here that cleared `skills` whenever the category
  // changed. It was a leftover from when skills were category-scoped *ids*
  // fetched per category, so a category switch genuinely invalidated the whole
  // set. They are names now -- any of the 654, plus custom ones the client
  // typed -- so there is nothing to invalidate, and clearing was pure loss.
  //
  // It was not merely annoying. `applyDraft` fills skills and then fills the
  // category, so the effect fired on the draft's own category and deleted the
  // skills a render later. The "filled in by the AI" badge is driven off a
  // separate `aiFilled` set, so it survived: the form said the AI had supplied
  // skills while displaying none, which is the worst combination available.
  //
  // A client picking a category after typing three skills lost them silently too.
  //
  // Consistency with the category is still worth having, and `suggestedSkills`
  // already has it -- the suggestions follow the category. The chosen skills do
  // not need to, because the chips are visible and removable.

  const suggestedSkills = skillsData
    .filter((skill) => {
      const skillCatId = typeof skill.category === 'object' ? String(skill.category.id) : String(skill.category);
      const isSameCategory = skillCatId === String(selectedCategory);
      const isNotSelected = !selectedSkills.includes(skill.name);
      const matchesInput = skill.name.toLowerCase().includes(skillInput.toLowerCase());
      return isSameCategory && isNotSelected && matchesInput;
    })
    .slice(0, 8);

  const addSkill = (skillName) => {
    if (selectedSkills.some(s => s.toLowerCase() === skillName.toLowerCase())) return;
    setValue("skills", [...selectedSkills, skillName], {
      shouldValidate: true,
    });
    setSkillInput("");
  };

  const handleAddSkill = () => {
    if (!skillInput.trim()) return;
    addSkill(skillInput.trim());
    setSkillInput("");
  };

  const onSubmit = (data) => {
    const formattedData = { ...data };
    // The API's DecimalAsStringJsonConverter requires money to arrive as JSON
    // strings; a bare number fails with "could not be converted to
    // System.Nullable<Decimal>". Keep the zod schema on numbers (the inputs use
    // valueAsNumber) and stringify only at the wire boundary.
    for (const k of ["fixed_price", "min_budget", "max_budget"]) {
      if (formattedData[k] !== null && formattedData[k] !== undefined) {
        formattedData[k] = String(formattedData[k]);
      }
    }
    if (data.pricing_type === "fixed") {
      delete formattedData.min_budget;
      delete formattedData.max_budget;
    } else {
      delete formattedData.fixed_price;
    }

    openModal("confirm-project", {
      data: formattedData,
      categories,
      onConfirm: async () => {
        try {
          // `await` is the whole point. The response body was never read and the
          // catch already reads `error.response`, so assigning it only invited the
          // reader to wonder what was supposed to happen to it.
          await createProject(formattedData);
          closeModal();
          navigate('/my-projects', { replace: true });
          toastSuccess("Project Posted Successfully");
        } catch (error) {
          closeModal();
          if (error.response) {
            toastError("Server error: " + JSON.stringify(error.response.data));
          } else {
            toastError("Unexpected error");
          }
        }
      },
    });
  };

  const Button = ({ children, ...props }) => <button {...props}>{children}</button>;

  // ── applying a draft ───────────────────────────────────────────────
  //
  // A draft is a suggestion, and the client is the author. So applying one:
  //
  //  - fills only the fields that are still empty, so pressing it twice, or
  //    pressing it after typing half the form, does not silently delete work;
  //  - merges skills rather than replacing them;
  //  - never sets a category id the form's own category list does not contain,
  //    because `category` is a `<select>` and an unknown value renders as blank
  //    and then fails validation with nothing pointing at the cause;
  //  - and tells the client what it did, so a form that looks unchanged is
  //    explained rather than confusing.
  const applyDraft = (draft) => {
    const parse = (raw) => {
      if (!raw) return null;
      if (typeof raw === "object") return raw;
      try {
        return JSON.parse(raw);
      } catch {
        return null;
      }
    };

    const parsed = parse(draft?.raw);
    if (!parsed) {
      toastError("That draft could not be read. Try generating it again.");
      return;
    }

    const current = getValues();
    const filled = [];

    /**
     * Fill a field, recording that the AI did it.
     *
     * `shouldDirty: false` is load-bearing. react-hook-form marks a field dirty
     * on any `setValue` unless told otherwise, and the AI badge is driven off
     * `isDirty`. Without this the badge would vanish the instant the draft was
     * applied -- the opposite of what it is for -- and there would be no way to
     * tell "the client typed here" from "the AI wrote here".
     *
     * `shouldValidate: true` is equally load-bearing, and was missing. The form
     * runs on react-hook-form's default `mode: "onSubmit"`, so errors appear
     * after a submit attempt and then sit there until something re-validates.
     * `setValue` without `shouldValidate` is exactly such a something-not: it
     * writes the value and leaves `formState.errors` untouched.
     *
     * The visible result was a form contradicting itself. A client pressed Post
     * with two skills, saw "Too small: expected array to have >=3 items" (the
     * schema's own `.min(3)`, with no custom message), then pressed "Help me
     * draft". The draft supplied three real skills -- and the error stayed on
     * screen, next to a now-correct field, telling the client their form was
     * broken when it was not. Nothing was actually wrong except the message.
     *
     * Only the filled fields are re-validated. A field this draft did not touch
     * keeps its error, which is right: the draft said nothing about it, so the
     * error is still true.
     */
    const fill = (field, value) => {
      setValue(field, value, { shouldDirty: false, shouldValidate: true });
      filled.push(field);
      setAiFilled((previous) => new Set([...previous, field]));
    };

    const setIfEmpty = (field, value) => {
      if (value === null || value === undefined || value === "") return;
      const existing = current[field];
      const isEmpty =
        existing === null ||
        existing === undefined ||
        existing === "" ||
        (Array.isArray(existing) && existing.length === 0) ||
        existing === 0;
      if (isEmpty) fill(field, value);
    };

    // The schema caps these at 600 characters, so a longer draft is truncated
    // here rather than left to fail validation with a message the client has no
    // way to act on -- they would have to delete words to make it submit.
    const cap = (text, max = 600) =>
      typeof text === "string" && text.trim() ? text.slice(0, max) : null;

    setIfEmpty("title", cap(parsed.title, 255));
    setIfEmpty("description", cap(parsed.description));
    setIfEmpty("requirements", cap(parsed.requirements));
    setIfEmpty("expected_deliverables", cap(parsed.expected_deliverables));

    // A new draft supersedes the old one's badges. Reset before filling, so a
    // field this draft does not touch loses a claim about content it no longer
    // describes.
    setAiFilled(new Set());

    // Names, not ids: the form holds skill *names* (`skills: ["React", "C#"]`).
    // The selection logic is in `draftSkills.js` because it decides whether the
    // AI's answer reaches the form at all, and it was untestable while it lived
    // inside this component.
    const skillOutcome = selectDraftSkills(
      parsed.skill_names,
      current.skills ?? [],
      skillsData.map((s) => s.name)
    );
    if (skillOutcome.catalogueEmpty) {
      // Distinct from "rejected", and said differently. The skill list failed to
      // load, so nothing was checked and nothing can be claimed -- claiming the
      // skills do not exist would be a confident statement about the catalogue
      // that nobody made.
      toastError(
        "Couldn't load the skill list, so the draft's skills were left off. Add them from the list below."
      );
    } else {
      if (skillOutcome.added.length > 0) {
        fill("skills", skillOutcome.merged);
      }
      if (skillOutcome.rejected.length > 0) {
        // Said out loud. A suggestion that silently fails to appear is
        // indistinguishable from the platform not having it.
        toastError(
          `Ignored ${skillOutcome.rejected.join(", ")} — not a skill on this platform.`
        );
      }
    }

    const categoryId = Number(parsed.category_id);
    if (
      Number.isFinite(categoryId) &&
      categories.some((c) => Number(c.id) === categoryId) &&
      (current.category === null || current.category === undefined || current.category === "")
    ) {
      fill("category", categoryId);
    }

    if (parsed.pricing_type === "fixed" || parsed.pricing_type === "range") {
      // Set directly rather than through `fill`: the pricing type is not a
      // suggestion to review, it is which set of budget boxes applies, and
      // overwriting the client's choice would be the draft deciding how they
      // price their own project.
      setValue("pricing_type", parsed.pricing_type, { shouldDirty: false });
      const price = Number(parsed.fixed_price_usd);
      const min = Number(parsed.min_budget_usd);
      const max = Number(parsed.max_budget_usd);
      if (parsed.pricing_type === "fixed" && Number.isFinite(price) && price > 0) {
        fill("fixed_price", price);
      } else if (parsed.pricing_type === "range" && Number.isFinite(min) && Number.isFinite(max) && max > min) {
        fill("min_budget", min);
        fill("max_budget", max);
      }
    }

    // The server caps delivery at 99 days, matching the schema. A draft that
    // says 120 would otherwise fail with "must be less than 100" and no way to
    // tell which of two numbers the client set was meant.
    const days = Number(parsed.delivery_days);
    if (Number.isFinite(days) && days > 0) {
      fill("delivery_days", Math.min(Math.round(days), 99));
    }

    if (filled.length === 0) {
      toastError("Nothing was filled in, because you had already written that part.");
    } else {
      toastSuccess(
        `Filled ${filled.length} ${filled.length === 1 ? "field" : "fields"}. ` +
          "Check the numbers before posting -- especially the price and the category."
      );
    }
  };

  /**
   * Whether a field's badge should read "AI", "you edited this", or nothing.
   *
   * Driven off react-hook-form's own `isDirty` rather than a separate tracker, so
   * it cannot fall out of step with the form. `applyDraft` fills with
   * `shouldDirty: false`, so dirty means the client typed.
   */
  const badgeFor = (field) => {
    if (!aiFilled.has(field)) return "none";
    return getFieldState(field).isDirty ? "edited" : "ai";
  };

  return (
    <>
      <div className="min-h-screen bg-surface-2 py-8 px-4 sm:px-6 lg:px-8">
        <div className="max-w-7xl mx-auto">
          <div className="text-center mb-8">
            <h1 className="text-[31px] font-bold text-fg leading-10 mb-2 font-inter">
              Post a New Project
            </h1>
            <p className="text-base text-fg-muted leading-6 font-roboto">
              Describe your project details to receive competitive proposals from
              our community of skilled freelancers.
            </p>
          </div>
          <div className="grid grid-cols-1 lg:grid-cols-12 gap-8">
            <div className="lg:col-span-8">
              <form onSubmit={handleSubmit(onSubmit, (errors) => console.log("Form Errors:", errors))}
                className="bg-surface rounded-xl shadow-[0_4px_6px_-4px_rgba(0,0,0,0.1),0_10px_15px_-3px_rgba(0,0,0,0.1)] p-8"
              >
                <div className="mb-8">
                  <Field label="Project Title" required error={errors.title?.message}>
                    {(f) => (
                      <input
                        type="text"
                        placeholder="e.g., Build a responsive e-commerce website"
                        {...register("title")}
                        {...f}
                        className="w-full px-3 h-[50px] text-base border-line-strong border-2 rounded-lg placeholder:text-fg-subtle"
                      />
                    )}
                  </Field>
                  <AiFieldBadge show={badgeFor("title") === "ai"} className="mt-1.5" />
                  <AiFieldEdited show={badgeFor("title") === "edited"} className="mt-1.5" />
                </div>

                <div className="mb-8">
                  <Field label="Project Description" required error={errors.description?.message}>
                    {(f) => (
                      <textarea
                        placeholder="Describe what you need..."
                        {...register("description")}
                        {...f}
                        className="w-full px-3 py-4 border-2 min-h-[170px] text-base border-line-strong rounded-lg placeholder:text-fg-subtle resize-none"
                      />
                    )}
                  </Field>

                  <AiFieldBadge show={badgeFor("description") === "ai"} className="mt-1.5" />
                  <AiFieldEdited show={badgeFor("description") === "edited"} className="mt-1.5" />

                  {/* Sits directly under the description because that is what it
                      reads. Pressed with an empty description it says so
                      instead of sending a request that cannot help. */}
                  <div className="mt-2 flex justify-end">
                    <DraftAssistButton
                      partialText={watch("description")}
                      onApply={applyDraft}
                    />
                  </div>
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
                        placeholder="Explain what you expect from the freelancer..."
                        {...register("requirements")}
                        {...f}
                        className="w-full px-3 py-4 border-2 min-h-[170px] text-base border-line-strong rounded-lg placeholder:text-fg-subtle resize-none"
                      />
                    )}
                  </Field>
                  <AiFieldBadge show={badgeFor("requirements") === "ai"} className="mt-1.5" />
                  <AiFieldEdited show={badgeFor("requirements") === "edited"} className="mt-1.5" />
                </div>

                <div className="mb-8">
                  <Field
                    label="Expected Deliverables"
                    required
                    error={errors.expected_deliverables?.message}
                  >
                    {(f) => (
                      <textarea
                        placeholder="List what the freelancer should deliver..."
                        {...register("expected_deliverables")}
                        {...f}
                        className="w-full px-3 py-4 border-2 min-h-[170px] text-base border-line-strong rounded-lg placeholder:text-fg-subtle resize-none"
                      />
                    )}
                  </Field>
                  <AiFieldBadge show={badgeFor("expected_deliverables") === "ai"} className="mt-1.5" />
                  <AiFieldEdited show={badgeFor("expected_deliverables") === "edited"} className="mt-1.5" />
                </div>

                <div className="mb-8">
                  {/* The old markup had `htmlFor='category'` on the label but no
                      matching `id` on the select, so the association was broken
                      in both directions. <Field> generates the id and wires it. */}
                  <Field label="Category" required error={errors.category?.message}>
                    {(f) => (
                      <select
                        {...register("category")}
                        {...f}
                        className="px-3 h-[47px] w-full border-2 border-line-strong rounded-lg"
                      >
                        <option value="">Select a category</option>
                        {categories.map((cat) => (
                          <option key={cat.id} value={cat.id}>{cat.name}</option>
                        ))}
                      </select>
                    )}
                  </Field>
                  <AiFieldBadge show={badgeFor("category") === "ai"} className="mt-1.5" />
                  <AiFieldEdited show={badgeFor("category") === "edited"} className="mt-1.5" />
                </div>

                <div className="mb-8">
                  {/* Left as a bare <label> when the rest of this form moved to
                      <Field>. It heads a set of skill chips, so a label could
                      never name it -- a label names one control. Same fix as
                      StepTwo's skills picker and EditProject's, so the three
                      forms now agree. */}
                  <fieldset>
                    <legend className="block mb-2">
                      <span className="text-xs font-medium text-fg-muted leading-5">
                        Required Skills
                      </span>
                      <span className="text-xs font-medium text-danger-soft-fg ml-1">*</span>
                    </legend>

                    <AiFieldBadge show={badgeFor("skills") === "ai"} className="mb-2" />
                    <AiFieldEdited show={badgeFor("skills") === "edited"} className="mb-2" />

                    <div className="flex gap-2">
                      {/* The legend consumes the visible heading, so this
                          control still needs its own name. */}
                      <label htmlFor="add-skill" className="sr-only">
                        Add a skill
                      </label>
                      <input
                        id="add-skill"
                        type="text"
                        placeholder="Type a skill"
                        value={skillInput}
                        onChange={(e) => setSkillInput(e.target.value)}
                        onKeyDown={(e) => {
                          if (e.key === "Enter") {
                            e.preventDefault();
                            handleAddSkill();
                          }
                        }}
                        className="w-full px-3 py-4 border-2 h-[50px] text-base border-line-strong rounded-lg flex-1"
                      />
                      <Button
                        type="button"
                        onClick={handleAddSkill}
                        className="px-6 py-4 border-2 h-[50px] text-sm font-medium text-fg-muted border-line-strong rounded-lg"
                      >
                        Add
                      </Button>
                    </div>

                    {errors.skills && (
                      <p className="text-danger-soft-fg text-xs mt-1">
                        {errors.skills.message ||
                          (Array.isArray(errors.skills) && errors.skills.find((err) => err)?.message)}
                      </p>
                    )}
                  {suggestedSkills.length > 0 && (
                    <div className="flex flex-wrap gap-2 mt-3">
                      {suggestedSkills.map((skill) => (
                        <button
                          key={skill.id}
                          type="button"
                          onClick={() => addSkill(skill.name)}
                          className="px-3 py-1 rounded-full text-sm bg-surface-3 text-fg-muted hover:bg-surface-inset border"
                        >
                          + {skill.name}
                        </button>
                      ))}
                    </div>
                  )}

                  {selectedSkills.length > 0 && (
                    <div className="flex flex-wrap gap-2 mt-3">
                      {selectedSkills.map((skill, index) => (
                        <span
                          key={index}
                          className="bg-brand-soft text-brand px-3 py-1 rounded-2xl text-sm flex items-center"
                        >
                          {skill}
                          <button
                            type="button"
                            onClick={() =>
                              setValue(
                                "skills",
                                selectedSkills.filter((_, i) => i !== index),
                                { shouldValidate: true }
                              )
                            }
                            className="ml-2 text-lg text-fg-subtle hover:text-danger-soft-fg"
                            aria-label={`Remove ${skill}`}
                          >
                            <span aria-hidden="true">×</span>
                          </button>
                        </span>
                      ))}
                    </div>
                  )}
                  </fieldset>
                </div>

                <div className="grid grid-cols-1 md:grid-cols-2 gap-6 mb-8">
                  <div className="mb-8">
                    {/* Two radios sharing one name: a group, so a legend. The
                        individual radios keep their own wrapping labels. */}
                    <fieldset>
                      <legend className="block mb-2 text-xs font-medium text-fg-muted">
                        Pricing Type *
                      </legend>
                      <div className="flex gap-4">
                        <label className="flex items-center gap-2 text-sm">
                          <input type="radio" value="fixed" {...register("pricing_type")} />
                          Fixed Price
                        </label>
                        <label className="flex items-center gap-2 text-sm">
                          <input type="radio" value="range" {...register("pricing_type")} />
                          Range Price
                        </label>
                      </div>
                      {errors.pricing_type && (
                        <p className="text-danger-soft-fg text-xs mt-1">{errors.pricing_type.message}</p>
                      )}
                    </fieldset>
                  </div>

                  {pricingType === "fixed" && (
                    <div className="mb-8">
                      <Field label="Fixed Budget" required error={errors.fixed_price?.message}>
                        {(f) => (
                          <input
                            type="number"
                            {...register("fixed_price", { valueAsNumber: true })}
                            {...f}
                            className="w-full px-3 h-[50px] border rounded-lg"
                          />
                        )}
                      </Field>
                      {/* The price is the number most worth a second look: a
                          plausible-looking wrong figure here is what a client
                          ends up agreeing to. */}
                      <AiFieldBadge show={badgeFor("fixed_price") === "ai"} className="mt-1.5" />
                      <AiFieldEdited show={badgeFor("fixed_price") === "edited"} className="mt-1.5" />
                    </div>
                  )}

                  {pricingType === "range" && (
                    <div className="grid grid-cols-2 gap-6 mb-8">
                      <Field label="Min Budget" required error={errors.min_budget?.message}>
                        {(f) => (
                          <input
                            type="number"
                            {...register("min_budget", { valueAsNumber: true })}
                            {...f}
                            className="w-full px-3 h-[50px] border rounded-lg"
                          />
                        )}
                      </Field>
                      <AiFieldBadge show={badgeFor("min_budget") === "ai"} className="mt-1.5" />
                      <AiFieldEdited show={badgeFor("min_budget") === "edited"} className="mt-1.5" />

                      <Field label="Max Budget" required error={errors.max_budget?.message}>
                        {(f) => (
                          <input
                            type="number"
                            {...register("max_budget", { valueAsNumber: true })}
                            {...f}
                            className="w-full px-3 h-[50px] border rounded-lg"
                          />
                        )}
                      </Field>
                      <AiFieldBadge show={badgeFor("max_budget") === "ai"} className="mt-1.5" />
                      <AiFieldEdited show={badgeFor("max_budget") === "edited"} className="mt-1.5" />
                    </div>
                  )}

                  <Field
                    label="Delivery Days"
                    required
                    error={errors.delivery_days?.message}
                    hint="Working days from contract signature to final delivery."
                  >
                    {(f) => (
                      <input
                        type="number"
                        {...register("delivery_days", { valueAsNumber: true })}
                        {...f}
                        // Clears the seeded 0 on focus so the user can type
                        // without having to select-all first. Preserved from the
                        // pre-Field markup.
                        onFocus={(e) => { if (e.target.value === "0") e.target.value = '' }}
                        className="w-full px-3 h-[50px] border rounded-lg"
                      />
                    )}
                  </Field>
                  <AiFieldBadge show={badgeFor("delivery_days") === "ai"} className="mt-1.5" />
                  <AiFieldEdited show={badgeFor("delivery_days") === "edited"} className="mt-1.5" />
                </div>

                <div className="mt-6">
                  <Button
                    type="submit"
                    disabled={isPending}
                    className="w-full h-[52px] bg-brand text-brand-fg text-sm font-medium rounded-lg disabled:opacity-60"
                  >
                    {isPending ? "Posting..." : "Post Project"}
                  </Button>
                </div>
              </form>
            </div>

            <div className="lg:col-span-4">
              <div className="bg-surface border border-brand-line rounded-lg p-6 sticky top-8">
                <div className="flex items-center gap-3 mb-6">
                  <Lightbulb className="w-6 h-6 text-brand" />
                  <h3 className="text-lg font-semibold text-fg leading-7 font-roboto">Tips for success</h3>
                </div>
                <div className="space-y-4">
                  <div className="flex items-start gap-3">
                    <CheckCircle2 className="w-6 h-6 text-success-soft-fg shrink-0 mt-0.5" />
                    <p className="text-base text-fg-muted leading-6 font-roboto">
                      <span className="font-medium text-fg-muted">Be specific:</span> Detailed descriptions attract better proposals.
                    </p>
                  </div>
                  <div className="flex items-start gap-3">
                    <CheckCircle2 className="w-6 h-6 text-success-soft-fg shrink-0 mt-0.5" />
                    <p className="text-base text-fg-muted leading-6 font-roboto">
                      <span className="font-medium text-fg-muted">Set a realistic budget:</span> Research market rates.
                    </p>
                  </div>
                  <div className="flex items-start gap-3">
                    <CheckCircle2 className="w-6 h-6 text-success-soft-fg shrink-0 mt-0.5" />
                    <p className="text-base text-fg-muted leading-6 font-roboto">
                      <span className="font-medium text-fg-muted">Define deliverables:</span> Clearly state what you expect.
                    </p>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </>
  )
}

export default AddProject;