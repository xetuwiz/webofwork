import { useMemo, useState } from "react";
import { Check, Search, Sparkles } from "lucide-react";
import { useCategories, useSkills } from "../../projectManagement/projectQueries.js";

export default function StepTwo({ values, onNext, onBack }) {
  const { data: categoriesData } = useCategories();
  const categories = useMemo(() => {
    if (Array.isArray(categoriesData?.results)) return categoriesData.results;
    if (Array.isArray(categoriesData)) return categoriesData;
    return [];
  }, [categoriesData]);

  const [selectedCategoryId, setSelectedCategoryId] = useState(values.primaryCategory ?? values.categoryId ?? categories[0]?.id ?? "");
  const [search, setSearch] = useState("");
  const [selectedSkills, setSelectedSkills] = useState(Array.isArray(values.skills) ? values.skills : []);

  const { data: skillsData } = useSkills(selectedCategoryId ? { categoryId: selectedCategoryId } : {});
  const skills = useMemo(() => {
    if (Array.isArray(skillsData?.results)) return skillsData.results;
    if (Array.isArray(skillsData)) return skillsData;
    return [];
  }, [skillsData]);

  const filteredSkills = useMemo(
    () => skills.filter((skill) => (skill.name ?? skill.customName ?? "").toLowerCase().includes(search.toLowerCase())),
    [skills, search],
  );

  const toggleSkill = (skill) => {
    const skillId = skill.id ?? skill.skillId ?? skill.customName ?? null;
    const skillEntry = { skillId, customName: skill.name ?? skill.customName ?? "" };
    setSelectedSkills((current) => {
      const exists = current.some((entry) => Number(entry.skillId ?? entry.id ?? 0) === Number(skillId ?? 0));
      return exists ? current.filter((entry) => Number(entry.skillId ?? entry.id ?? 0) !== Number(skillId ?? 0)) : [...current, skillEntry];
    });
  };

  const onSubmit = () => {
    if (!selectedCategoryId || selectedSkills.length === 0) return;
    onNext({
      primaryCategory: Number(selectedCategoryId),
      categoryId: Number(selectedCategoryId),
      skills: selectedSkills,
    });
  };

  return (
    <div className="mx-auto max-w-4xl space-y-6">
      <div className="space-y-2">
        <p className="text-xs font-semibold uppercase tracking-[0.2em] text-blue-600">Step 2</p>
        <h2 className="text-3xl font-bold text-slate-900">Choose your services</h2>
        <p className="text-sm text-slate-600">Select a category and the skills you want clients to discover.</p>
      </div>

      <div className="grid gap-6 rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
        <div>
          <h3 className="mb-3 text-sm font-semibold uppercase tracking-[0.15em] text-slate-500">Category</h3>
          <div className="flex flex-wrap gap-2">
            {categories.map((category) => (
              <button
                key={category.id}
                type="button"
                onClick={() => setSelectedCategoryId(category.id)}
                className={`rounded-full border px-4 py-2 text-sm font-medium ${
                  Number(selectedCategoryId) === Number(category.id)
                    ? "border-blue-600 bg-blue-600 text-white"
                    : "border-slate-200 bg-slate-50 text-slate-700 hover:bg-slate-100"
                }`}
              >
                {category.name}
              </button>
            ))}
          </div>
        </div>

        <div>
          <div className="mb-3 flex items-center justify-between">
            <h3 className="text-sm font-semibold uppercase tracking-[0.15em] text-slate-500">Skills</h3>
            <span className="text-xs text-slate-500">{selectedSkills.length} selected</span>
          </div>

          <div className="relative mb-4">
            <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
            <input
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              placeholder="Search skills..."
              className="w-full rounded-xl border border-slate-200 bg-white pl-10 pr-3 py-2.5 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100"
            />
          </div>

          <div className="flex flex-wrap gap-2">
            {filteredSkills.length > 0 ? (
              filteredSkills.map((skill) => {
                const skillId = skill.id ?? skill.skillId ?? skill.customName ?? "";
                const isSelected = selectedSkills.some((entry) => Number(entry.skillId ?? entry.id ?? 0) === Number(skillId ?? 0));
                return (
                  <button
                    key={skillId}
                    type="button"
                    onClick={() => toggleSkill(skill)}
                    className={`inline-flex items-center gap-2 rounded-full border px-3 py-2 text-sm ${
                      isSelected ? "border-blue-600 bg-blue-50 text-blue-700" : "border-slate-200 bg-slate-50 text-slate-700 hover:bg-slate-100"
                    }`}
                  >
                    {isSelected ? <Check className="h-4 w-4" /> : <Sparkles className="h-4 w-4" />}
                    {skill.name ?? skill.customName}
                  </button>
                );
              })
            ) : (
              <p className="text-sm text-slate-500">No skills available for the selected category yet.</p>
            )}
          </div>
        </div>
      </div>

      <div className="flex items-center justify-between pt-2">
        <button type="button" onClick={onBack} className="rounded-xl border border-slate-200 bg-white px-4 py-2.5 text-sm font-medium text-slate-600 hover:bg-slate-100">
          Back
        </button>
        <button
          type="button"
          disabled={!selectedCategoryId || selectedSkills.length === 0}
          onClick={onSubmit}
          className="rounded-xl bg-blue-600 px-5 py-2.5 text-sm font-semibold text-white hover:bg-blue-700 disabled:cursor-not-allowed disabled:bg-slate-300"
        >
          Continue
        </button>
      </div>
    </div>
  );
}
