import { useState, useEffect } from "react";
import { X, Plus, Search } from "lucide-react";
import { useCategories } from "../projectManagement/projectQueries";
import { useProfile } from "./profileQueries";
import { useUpdateProfile } from "./profileMutations";
import { toastError, toastSuccess } from "../../../utils/toast";

export default function Interests() {
  const { data: categoriesData } = useCategories();
  const { data: profile } = useProfile();
  const { mutate: updateProfile, isLoading } = useUpdateProfile();

  const categories = categoriesData?.results || [];

  const [selected, setSelected] = useState([]);
  const [searchQuery, setSearchQuery] = useState("");

  useEffect(() => {
    if (profile?.categories && categories.length > 0) {
      const initialSelected = categories.filter(cat => 
        profile.categories.includes(cat.name)
      );
      setSelected(initialSelected);
    }
  }, [profile, categories]);

  const handleSave = () => {
    updateProfile(
      {
        categories: selected.map((c) => c.id),
      },
      {
        onSuccess: () => {
          toastSuccess("Updated successfully");
        },
        onError: (error) => {
          toastError("Failed to update:", error);
        }
      }
    );
  };

  const filteredCategories = categories.filter((cat) =>
    cat.name.toLowerCase().includes(searchQuery.toLowerCase())
  );

  const popularCategories = categories.slice(0, 10);

  const addCategory = (category) => {
    if (!selected.find((c) => c.id === category.id)) {
      setSelected([...selected, category]);
    }
    setSearchQuery("");
  };

  const removeCategory = (category) => {
    setSelected(selected.filter((c) => c.id !== category.id));
  };



  return (
    <div className="min-h-screen bg-surface py-8 px-4 sm:px-6 lg:px-8">
      <div className="max-w-page-md mx-auto">
        <h1 className="text-[26px] font-bold text-fg mb-8">
          Skills & Interests
        </h1>

        <div className="space-y-6">
          <div className="bg-surface border border-line-soft rounded-xl shadow p-8">
            <div className="mb-6">
              <h2 className="text-[15px] font-semibold text-fg mb-2">
                Current Interests
              </h2>
              <p className="text-xs text-fg-subtle">
                These skills appear on your profile and help match you with the
                right freelancers.
              </p>
            </div>

            <div className="flex flex-wrap gap-3">
              {selected.length > 0 ? (
                selected.map((cat) => (
                  <InterestTag
                    key={cat.id}
                    label={cat.name}
                    onRemove={() => removeCategory(cat)}
                  />
                ))
              ) : (
                <p className="text-xs text-fg-subtle">
                  No skills selected yet.
                </p>
              )}
            </div>
          </div>

          <div className="bg-surface border border-line-soft rounded-xl shadow p-8">
            <div className="mb-6">
              <h2 className="text-[15px] font-semibold text-fg mb-2">
                Add New Interests
              </h2>
              <p className="text-xs text-fg-subtle">
                Search or choose from popular categories below.
              </p>
            </div>

            <div className="relative mb-8">
              <Search className="absolute left-4 top-1/2 -translate-y-1/2 w-5 h-5 text-fg-subtle" />
              <input
                type="text"
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                placeholder="Search categories..."
                className="w-full h-[50px] pl-12 pr-4 border border-line rounded-lg focus-visible:ring-2 focus-visible:ring-brand"
              />
            </div>

            <div>
              <h3 className="text-xs font-medium text-fg-muted mb-4">
                Popular Categories
              </h3>

              <div className="flex flex-wrap gap-3">
                {(searchQuery ? filteredCategories : popularCategories).filter((cat)=>!selected.find((s) => s.id === cat.id)).map(
                  (cat) => (
                    <SkillButton
                      key={cat.id}
                      label={cat.name}
                      onClick={() => addCategory(cat)}
                    />
                  )
                )}
              </div>
            </div>
          </div>
        </div>

        <div className="flex items-center justify-between mt-8">
          <p className="text-xs text-fg-subtle">
            {selected.length} skills selected
          </p>

          <button
            onClick={handleSave}
            disabled={isLoading}
            className="bg-brand text-white px-8 py-[15px] rounded-lg text-[15px] font-medium hover:bg-brand transition disabled:opacity-50"
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
    <span className="inline-flex items-center gap-2 bg-brand-soft border border-brand-line rounded-full px-[17px] py-[10px] text-xs font-medium text-brand">
      {label}
      <button
        onClick={onRemove}
        className="w-4 h-4 rounded-full bg-brand-soft flex items-center justify-center hover:bg-brand-soft"
      >
        <X className="w-3 h-3 text-brand" />
      </button>
    </span>
  );
}

function SkillButton({ label, onClick, isAdded }) {
  return (
    <button
      onClick={onClick}
      className={`inline-flex items-center gap-2 rounded-full px-[17px] py-[10px] text-xs font-medium border transition ${
        isAdded
          ? "bg-surface-2 border-line text-fg-subtle cursor-not-allowed"
          : "bg-surface-2 border-line text-fg-muted hover:bg-surface-2"
      }`}
    >
      <Plus className="w-4 h-4" />
      {label}
    </button>
  );
}
