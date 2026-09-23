import { useMemo, useState } from "react";
import { X, Plus, Search } from "lucide-react";
import toast from "react-hot-toast";
import { useCategories } from "../projectManagement/projectQueries.js";
import { useProfile } from "./profileQueries.js";
import { useUpdateProfile } from "./profileMutations.js";

export default function Interests() {
  const { data: categoriesData } = useCategories();
  const { data: profile } = useProfile();
  const { mutate: updateProfile, isPending } = useUpdateProfile();

  const categories = useMemo(() => categoriesData?.results ?? [], [categoriesData]);
  const [selected, setSelected] = useState([]);
  const [searchQuery, setSearchQuery] = useState("");

  const initialSelectedIds = useMemo(() => {
    if (!profile?.categories || categories.length === 0) return new Set();
    return new Set(
      categories
        .filter((cat) => profile.categories.includes(cat.name) || profile.categories.includes(cat.id))
        .map((cat) => cat.id),
    );
  }, [profile, categories]);

  const derivedSelected = useMemo(() => {
    const byId = new Map();
    categories.filter((category) => initialSelectedIds.has(category.id)).forEach((category) => byId.set(category.id, category));
    selected.forEach((category) => byId.set(category.id, category));
    return Array.from(byId.values());
  }, [categories, initialSelectedIds, selected]);

  const handleSave = () => {
    updateProfile(
      { categories: derivedSelected.map((category) => category.id) },
      {
        onSuccess: () => toast.success("Updated successfully"),
        onError: () => toast.error("Failed to update interests"),
      },
    );
  };

  const filteredCategories = categories.filter((category) => category.name?.toLowerCase().includes(searchQuery.toLowerCase()));
  const popularCategories = categories.slice(0, 10);

  const addCategory = (category) => {
    if (!selected.find((item) => item.id === category.id)) {
      setSelected((current) => [...current, category]);
    }
    setSearchQuery("");
  };

  const removeCategory = (category) => {
    setSelected((current) => current.filter((item) => item.id !== category.id));
  };

  return (
    <div className="min-h-screen bg-white py-8 px-4 sm:px-6 lg:px-8">
      <div className="max-w-[800px] mx-auto">
        <h1 className="text-[26px] font-bold text-[#111827] mb-8">Skills & Interests</h1>

        <div className="space-y-6">
          <div className="bg-white border border-[#F3F4F6] rounded-xl shadow p-8">
            <div className="mb-6">
              <h2 className="text-[15px] font-semibold text-[#111827] mb-2">Current Interests</h2>
              <p className="text-xs text-[#6B7280]">These skills appear on your profile and help match you with the right freelancers.</p>
            </div>

            <div className="flex flex-wrap gap-3">
              {derivedSelected.length > 0 ? (
                derivedSelected.map((cat) => <InterestTag key={cat.id} label={cat.name} onRemove={() => removeCategory(cat)} />)
              ) : (
                <p className="text-xs text-gray-500">No skills selected yet.</p>
              )}
            </div>
          </div>

          <div className="bg-white border border-[#F3F4F6] rounded-xl shadow p-8">
            <div className="mb-6">
              <h2 className="text-[15px] font-semibold text-[#111827] mb-2">Add New Interests</h2>
              <p className="text-xs text-[#6B7280]">Search or choose from popular categories below.</p>
            </div>

            <div className="relative mb-8">
              <Search className="absolute left-4 top-1/2 -translate-y-1/2 w-5 h-5 text-[#9CA3AF]" />
              <input
                type="text"
                value={searchQuery}
                onChange={(event) => setSearchQuery(event.target.value)}
                placeholder="Search categories..."
                className="w-full h-[50px] pl-12 pr-4 border border-[#E5E7EB] rounded-lg focus:ring-2 focus:ring-blue-500"
              />
            </div>

            <div>
              <h3 className="text-xs font-medium text-[#374151] mb-4">Popular Categories</h3>
              <div className="flex flex-wrap gap-3">
                {(searchQuery ? filteredCategories : popularCategories)
                  .filter((cat) => !derivedSelected.find((item) => item.id === cat.id))
                  .map((cat) => (
                    <SkillButton key={cat.id} label={cat.name} onClick={() => addCategory(cat)} />
                  ))}
              </div>
            </div>
          </div>
        </div>

        <div className="flex items-center justify-between mt-8">
          <p className="text-xs text-[#6B7280]">{derivedSelected.length} skills selected</p>
          <button
            type="button"
            onClick={handleSave}
            disabled={isPending}
            className="bg-[#3B82F6] text-white px-8 py-[15px] rounded-lg text-[15px] font-medium hover:bg-[#2563EB] transition disabled:opacity-50"
          >
            Save Interests
          </button>
        </div>
      </div>
    </div>
  );
}

function InterestTag({ label, onRemove }) {
  return (
    <span className="inline-flex items-center gap-2 bg-[#EFF6FF] border border-[#DBEAFE] rounded-full px-[17px] py-[10px] text-xs font-medium text-[#3B82F6]">
      {label}
      <button type="button" onClick={onRemove} className="w-4 h-4 rounded-full bg-[#DBEAFE] flex items-center justify-center hover:bg-[#BFDBFE]">
        <X className="w-3 h-3 text-[#3B82F6]" />
      </button>
    </span>
  );
}

function SkillButton({ label, onClick }) {
  return (
    <button
      type="button"
      onClick={onClick}
      className="inline-flex items-center gap-2 rounded-full px-[17px] py-[10px] text-xs font-medium border transition bg-[#F9FAFB] border-[#E5E7EB] text-[#374151] hover:bg-[#F3F4F6]"
    >
      <Plus className="w-4 h-4" />
      {label}
    </button>
  );
}
