import { useState } from "react";
import { LayoutGrid, Wrench } from "lucide-react";
import { AddCategoryModal } from "./components/AddCategoryModal.jsx";
import { DeleteCategoryModal } from "./components/DeleteCategoryModal.jsx";
import { EditCategoryModal } from "./components/EditCategoryModal.jsx";
import AddSkillModal from "./components/AddSkillsModal.jsx";
import DeleteSkillModal from "./components/DeleteSkillModal.jsx";
import EditSkillModal from "./components/EditSkillsModal.jsx";
import { ManagementList } from "./ManagementList.jsx";
import { useAllCategories, useCategories, useSkills } from "./categoryQueries.js";
import { useModal } from "../../../hooks/modal/useModalStore.jsx";

export default function CategoriesAndSkills() {
  const { modal, modalProps, closeModal, openModal } = useModal();
  const [categoryPage, setCategoryPage] = useState(1);
  const [skillPage, setSkillPage] = useState(1);
  const [categoryInput, setCategoryInput] = useState("");
  const [skillInput, setSkillInput] = useState("");
  const [categorySearch, setCategorySearch] = useState("");
  const [skillSearch, setSkillSearch] = useState("");

  const { data: categoryData, isLoading: catLoading } = useCategories({ page: categoryPage, search: categorySearch });
  const { data: allCategoriesData = [], isLoading: categoriesListLoading } = useAllCategories();
  const { data: skillData, isLoading: skillLoading } = useSkills({ page: skillPage, search: skillSearch });

  if ((catLoading && !categoryData) || (skillLoading && !skillData)) {
    return (
      <div className="flex min-h-screen items-center justify-center text-sm text-slate-500">
        Loading management data...
      </div>
    );
  }

  const categories = Array.isArray(categoryData) ? categoryData : categoryData?.results ?? [];
  const filteredCategories = categories.filter((category) =>
    category.name.toLowerCase().includes(categorySearch.toLowerCase()),
  );

  const skills = Array.isArray(skillData) ? skillData : skillData?.results ?? [];
  const filteredSkills = skills.filter((skill) => skill.name.toLowerCase().includes(skillSearch.toLowerCase()));

  return (
    <div className="min-h-screen bg-slate-50 p-8 md:p-12">
      <div className="mx-auto max-w-5xl">
        <ManagementList
          title="Categories"
          icon={LayoutGrid}
          items={filteredCategories}
          iconColor="blue"
          searchValue={categoryInput}
          onSearchChange={setCategoryInput}
          onSearchSubmit={() => {
            setCategorySearch(categoryInput.trim());
            setCategoryPage(1);
          }}
          currentPage={categoryPage}
          totalCount={filteredCategories.length}
          onNext={() => setCategoryPage((page) => page + 1)}
          onPrev={() => setCategoryPage((page) => Math.max(page - 1, 1))}
          hasNext={false}
          hasPrev={categoryPage > 1}
          onAdd={() => openModal("add-category")}
          onEdit={(item) => openModal("edit-category", item)}
          onDelete={(item) => openModal("delete-category", item)}
        />

        <ManagementList
          title="Skills"
          icon={Wrench}
          items={filteredSkills}
          iconColor="purple"
          searchValue={skillInput}
          onSearchChange={setSkillInput}
          onSearchSubmit={() => {
            setSkillSearch(skillInput.trim());
            setSkillPage(1);
          }}
          currentPage={skillPage}
          totalCount={filteredSkills.length}
          onNext={() => setSkillPage((page) => page + 1)}
          onPrev={() => setSkillPage((page) => Math.max(page - 1, 1))}
          hasNext={false}
          hasPrev={skillPage > 1}
          onAdd={() => openModal("add-skill", { allCategories: allCategoriesData })}
          onEdit={(item) => openModal("edit-skill", { item, allCategories: allCategoriesData })}
          onDelete={(item) => openModal("delete-skill", item)}
        />
      </div>

      {modal === "add-category" && <AddCategoryModal open onOpenChange={(value) => !value && closeModal()} />}
      {modal === "edit-category" && <EditCategoryModal open onOpenChange={(value) => !value && closeModal()} category={modalProps} />}
      {modal === "delete-category" && <DeleteCategoryModal open onOpenChange={(value) => !value && closeModal()} category={modalProps} />}
      {modal === "add-skill" && (
        <AddSkillModal categories={allCategoriesData} isLoading={categoriesListLoading} onClose={closeModal} />
      )}
      {modal === "edit-skill" && (
        <EditSkillModal open onOpenChange={(value) => !value && closeModal()} skill={modalProps.item} categories={modalProps.allCategories ?? []} />
      )}
      {modal === "delete-skill" && <DeleteSkillModal open onOpenChange={(value) => !value && closeModal()} skill={modalProps} />}
    </div>
  );
}
