import { useState } from "react";
import { LayoutGrid, Wrench } from "lucide-react";
import { ManagementList } from "./ManagementList";
import { useAllCategories, useCategories } from "./categoryQueries";
import { useSkills } from "./skillQueries";
import { useModal } from "../../../hooks/modal/useModalStore";
import { useListState, toQueryParams } from "../../../hooks/useListState";

export default function CategoriesAndSkills() {
  const { openModal } = useModal();

  // Two independent lists on one page, so each gets its own URL namespace.
  // Without the prefixes both would read and write `page`, and paging the
  // categories would silently reset the skills to page 1.
  const categories = useListState({ prefix: "cat_", defaults: { search: "" } });
  const skills = useListState({ prefix: "skill_", defaults: { search: "" } });

  // The raw inputs are what the fields show; the committed terms are what the
  // queries and the URL use. Submit-driven rather than debounced, so typing does
  // not fire a request per keystroke.
  const [categoryInput, setCategoryInput] = useState(categories.filters.search);
  const [skillInput, setSkillInput] = useState(skills.filters.search);

  // Re-sync the fields when the URL changes underneath them (Back/Forward).
  // Render-time adjustment, which React documents for exactly this case.
  const [lastCat, setLastCat] = useState(categories.filters.search);
  if (categories.filters.search !== lastCat) {
    setLastCat(categories.filters.search);
    setCategoryInput(categories.filters.search);
  }
  const [lastSkill, setLastSkill] = useState(skills.filters.search);
  if (skills.filters.search !== lastSkill) {
    setLastSkill(skills.filters.search);
    setSkillInput(skills.filters.search);
  }

  const { data: categoryData, isLoading: catLoading } = useCategories(
    toQueryParams({ search: categories.filters.search }, categories.page, categories.pageSize),
  );

  const allCategories = useAllCategories();

  const { data: skillData, isLoading: skillLoading } = useSkills(
    toQueryParams({ search: skills.filters.search }, skills.page, skills.pageSize),
  );

  if ((catLoading && !categoryData) || (skillLoading && !skillData)) {
    return (
      <div className="min-h-screen flex items-center justify-center text-sm text-fg-subtle">
        Loading management data...
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-surface-2 p-8 md:p-12">
      <div className="max-w-5xl mx-auto">
        <ManagementList
          title="Categories"
          itemNoun="categories"
          icon={LayoutGrid}
          items={categoryData?.results ?? []}
          count={categoryData?.count ?? 0}
          iconColor="blue"
          searchValue={categoryInput}
          onSearchChange={setCategoryInput}
          onSearchSubmit={() => categories.set("search", categoryInput.trim())}
          hasActiveSearch={!!categories.filters.search}
          onClearSearch={() => {
            setCategoryInput("");
            categories.set("search", "");
          }}
          page={categories.page}
          pageSize={categories.pageSize}
          onPageChange={categories.setPage}
          onPageSizeChange={categories.setPageSize}
          onAdd={() => openModal("add-category")}
          onEdit={(item) => openModal("edit-category", item)}
          onDelete={(item) => openModal("delete-category", item)}
        />

        <ManagementList
          title="Skills"
          itemNoun="skills"
          icon={Wrench}
          items={skillData?.results ?? []}
          count={skillData?.count ?? 0}
          iconColor="purple"
          searchValue={skillInput}
          onSearchChange={setSkillInput}
          onSearchSubmit={() => skills.set("search", skillInput.trim())}
          hasActiveSearch={!!skills.filters.search}
          onClearSearch={() => {
            setSkillInput("");
            skills.set("search", "");
          }}
          page={skills.page}
          pageSize={skills.pageSize}
          onPageChange={skills.setPage}
          onPageSizeChange={skills.setPageSize}
          onAdd={() => openModal("add-skill", { allCategories })}
          onEdit={(item) => openModal("edit-skill", { item, allCategories })}
          onDelete={(item) => openModal("delete-skill", item)}
        />
      </div>
    </div>
  );
}
