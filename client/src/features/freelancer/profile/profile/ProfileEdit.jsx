import React, { useState, useEffect } from "react";
import { Plus, Pencil, Trash2, Camera, X, Check, Loader2 } from "lucide-react";
import { uploadToCloudinary } from "../../../utils/cloudinaryHelper";
import { useFreelancerProfile } from "./profileQueries";
import { useUpdateFreelancerProfile } from "./profileMutation";
import {
  createPortfolioItem,
  updatePortfolioItem,
  deletePortfolioItem,
} from "./profileApi";
import { useAllCategories } from "../../client/projectManagement/projectQueries";
import PortfolioModal from "./PortfolioModal"; 
import { useNavigate } from "react-router-dom";
import { Field } from "../../../components/common/Field";

const SkillTag = ({ skill, onRemove }) => (
  <div className="flex items-center gap-1.5 px-3 py-1.5 rounded-full border border-brand-line bg-brand-soft">
    <span className="text-xs font-medium text-brand leading-5">{skill}</span>
    <button onClick={onRemove} className="p-0.5 rounded-full hover:bg-brand-hover-soft transition-colors">
      <X className="w-3.5 h-3.5 text-brand" />
    </button>
  </div>
);

const PackageCard = ({ title, data, onChange }) => (
  <div className="flex flex-col gap-4 p-5 rounded-xl border-2 border-brand-line bg-brand-soft/30">
    <h3 className="text-sm font-semibold text-fg leading-6">{title}</h3>
    <div className="flex flex-col gap-4">
      {/* These were <label>s sitting above their input with no htmlFor and no
          id on the input, so all three named nothing. Because this card is
          rendered once per pricing tier, the same three nameless fields were
          repeated for Basic, Standard and Premium -- nine unlabelled inputs in
          total. <Field> generates the id per instance, which is why it is the
          right fix here and a hand-written htmlFor would not be. */}
      <Field label="Price ($)">
        {(f) => (
          <input
            type="number"
            value={data.price}
            onChange={(e) => onChange("price", e.target.value)}
            {...f}
            className="h-[46px] px-4 rounded-lg border border-line-strong bg-surface"
          />
        )}
      </Field>
      <Field label="Delivery Days">
        {(f) => (
          <input
            type="number"
            value={data.deliveryTime}
            onChange={(e) => onChange("deliveryTime", e.target.value)}
            {...f}
            className="h-[46px] px-4 rounded-lg border border-line-strong bg-surface"
          />
        )}
      </Field>
      <Field label="Description">
        {(f) => (
          <textarea
            value={data.description}
            onChange={(e) => onChange("description", e.target.value)}
            rows={4}
            {...f}
            className="px-4 py-3 rounded-lg border border-line-strong bg-surface resize-none"
          />
        )}
      </Field>
    </div>
  </div>
);

export default function ProfileEdit() {
  const { data: initialData, isLoading } = useFreelancerProfile();
  const { mutateAsync: update, isPending } = useUpdateFreelancerProfile();
  const { data: categoriesData } = useAllCategories();

  const [profile, setProfile] = useState({
    fullName: "",
    tagline: "",
    bio: "",
    skills: [],
    isAvailable: true,
    profilePhoto: null,
    // Neither experienceLevel nor availability had any input in this form, so they
    // were never editable even though the API and DTO both support them.
    experienceLevel: "",
    categoryId: "",
  });

  const [packages, setPackages] = useState({
    starter: { price: "", deliveryTime: "", description: "" },
    pro: { price: "", deliveryTime: "", description: "" },
  });

  const [portfolios, setPortfolios] = useState([]);
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [editingItem, setEditingItem] = useState(null);
  const [newSkill, setNewSkill] = useState("");
  const [isUploading, setIsUploading] = useState(false);

  useEffect(() => {
    if (initialData) {
      setProfile({
        fullName: initialData.full_name || "",
        tagline: initialData.tagline || "",
        bio: initialData.bio || "",
        skills: initialData.skills || [],
        isAvailable: initialData.availability ?? true,
        experienceLevel: initialData.experience_level || "intermediate",
        categoryId: initialData.category || "",
        profilePhoto: initialData.profile_photo || null,
      });

      if (initialData.packages) {
        setPackages({
          starter: {
            price: initialData.packages.starter?.price || "",
            deliveryTime: initialData.packages.starter?.delivery_days || "",
            description: Array.isArray(initialData.packages.starter?.description)
              ? initialData.packages.starter.description.join("\n")
              : initialData.packages.starter?.description || "",
          },
          pro: {
            price: initialData.packages.pro?.price || "",
            deliveryTime: initialData.packages.pro?.delivery_days || "",
            description: Array.isArray(initialData.packages.pro?.description)
              ? initialData.packages.pro.description.join("\n")
              : initialData.packages.pro?.description || "",
          },
        });
      }

      setPortfolios(initialData.portfolios || []);
    }
  }, [initialData]);

  const handlePackageChange = (tier, field, value) => {
    setPackages((prev) => ({
      ...prev,
      [tier]: { ...prev[tier], [field]: value },
    }));
  };
  const navigate = useNavigate()
  
  const handlePhotoUpload = async (e) => {
    const file = e.target.files[0];
    if (!file) return;
    setIsUploading(true);
    try {
      const data = await uploadToCloudinary(file);
      setProfile((prev) => ({ ...prev, profilePhoto: data.secure_url }));
    } catch (err) {
      alert("Photo upload failed");
    } finally {
      setIsUploading(false);
    }
  };

  const handleOpenAddModal = () => {
    setEditingItem(null);
    setIsModalOpen(true);
  };

  const handleOpenEditModal = (item) => {
    setEditingItem(item);
    setIsModalOpen(true);
  };

  const handleSavePortfolioItem = async (itemData) => {
    try {
      if (editingItem) {
        // PATCH /api/profile/freelancer/portfolio/{id} returns the saved row,
        // whose shape matches the GET (`id`, `title`, `description`, `image_url`).
        const saved = await updatePortfolioItem(editingItem.id, itemData);
        setPortfolios((prev) =>
          prev.map((p) => (p.id === editingItem.id ? { ...saved } : p))
        );
      } else {
        // POST returns only { id }; carry the entered fields alongside it so the
        // grid has an image/title without a round trip.
        const created = await createPortfolioItem(itemData);
        setPortfolios((prev) => [...prev, { ...itemData, id: created.id }]);
      }
      setIsModalOpen(false);
    } catch {
      // The axios interceptor toasts the real error; keep the modal open so the
      // entered data is not lost.
    }
  };

  const handleRemovePortfolio = async (id) => {
    try {
      await deletePortfolioItem(id);
      setPortfolios((prev) => prev.filter((item) => item.id !== id));
    } catch {
      // Leave the item in place; the interceptor surfaces the failure.
    }
  };

  const handleSaveChanges = async () => {
    // `profile_photo` and `portfolios` are deliberately absent: the backend
    // DTO (UpdateFreelancerProfileRequest) has no fields for them, so sending
    // them made the save claim work it then discarded. The profile photo is a
    // Cloudinary preview, and portfolio rows are persisted through their own
    // item endpoints above.
    const payload = {
      full_name: profile.fullName,
      tagline: profile.tagline,
      bio: profile.bio,
      skills: profile.skills,
      availability: profile.isAvailable,
      experience_level: profile.experienceLevel,
      // Was never sent, so the category was fixed at onboarding forever.
      category_id: profile.categoryId ? Number(profile.categoryId) : null,
      packages: {
        starter: {
          price: packages.starter.price,
          delivery_days: packages.starter.deliveryTime,
          description: packages.starter.description,
        },
        pro: {
          price: packages.pro.price,
          delivery_days: packages.pro.deliveryTime,
          description: packages.pro.description,
        },
      },
    };
    try {
      await update(payload);
      navigate(-1);
    } catch {
      // onError already toasts; stay on the form so the edit is not lost.
    }
  };

  if (isLoading) {
    return (
      <div className="min-h-screen flex items-center justify-center">
        <div className="animate-spin rounded-full h-12 w-12 border-t-2 border-b-2 border-blue-500"></div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-surface-2 py-8 px-4 sm:px-6">
      <div className="max-w-page-lg mx-auto">
        <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4 mb-8">
          <div>
            <h1 className="text-2xl font-bold text-fg">Edit Professional Profile</h1>
            <p className="text-sm text-fg-subtle">Update your details to attract more clients.</p>
          </div>
          <button
            onClick={handleSaveChanges}
            disabled={isPending || isUploading}
            className="flex items-center justify-center gap-2 px-6 py-3 rounded-xl bg-brand text-brand-fg text-sm font-bold hover:bg-brand-hover disabled:opacity-50 transition-all"
          >
            {isPending ? "Saving..." : <><Check className="w-4 h-4" /> Save Changes</>}
          </button>
        </div>

        <div className="space-y-6">
          <section className="p-6 bg-surface rounded-2xl border border-line shadow-sm">
            <h2 className="text-lg font-bold mb-6 text-fg">Basic Information</h2>
            <div className="flex flex-col md:flex-row gap-8">
              <div className="relative group">
                <div className="w-24 h-24 rounded-2xl bg-brand-soft flex items-center justify-center overflow-hidden border-2 border-line-soft relative">
                  {isUploading && (
                    <div className="absolute inset-0 z-10 bg-surface/60 flex items-center justify-center">
                       <Loader2 className="w-6 h-6 text-brand animate-spin" />
                    </div>
                  )}
                  {profile.profilePhoto ? (
                    <img src={profile.profilePhoto} className="w-full h-full object-cover" alt="Profile" />
                  ) : (
                    <span className="text-2xl font-bold text-brand">{profile.fullName ? profile.fullName[0] : "?"}</span>
                  )}
                </div>
                <label className="absolute top-18 -right-2 p-2 bg-surface rounded-full shadow-lg border cursor-pointer hover:bg-surface-2">
                  <Camera className="w-4 h-4 text-fg-muted" />
                  <input type="file" className="hidden" onChange={handlePhotoUpload} accept="image/*" disabled={isUploading} />
                </label>
              </div>

              <div className="flex-1 grid grid-cols-1 md:grid-cols-2 gap-4">
                {/* These five were <label>s above their control with no htmlFor
                    and no id on the control. The checkbox below is the correct
                    pattern already -- it wraps its input -- and is left alone. */}
                <Field label="Full Name">
                  {(f) => (
                    <input
                      {...f}
                      className="w-full h-12 px-4 rounded-xl border border-line focus-visible:ring-2 focus-visible:ring-brand outline-none"
                      value={profile.fullName}
                      onChange={(e) => setProfile({ ...profile, fullName: e.target.value })}
                    />
                  )}
                </Field>
                <Field label="Tagline">
                  {(f) => (
                    <input
                      {...f}
                      className="w-full h-12 px-4 rounded-xl border border-line focus-visible:ring-2 focus-visible:ring-brand outline-none"
                      value={profile.tagline}
                      onChange={(e) => setProfile({ ...profile, tagline: e.target.value })}
                    />
                  )}
                </Field>
                <div className="md:col-span-2">
                  <Field label="Bio">
                    {(f) => (
                      <textarea
                        {...f}
                        className="w-full p-4 rounded-xl border border-line focus-visible:ring-2 focus-visible:ring-brand outline-none min-h-[120px]"
                        value={profile.bio}
                        onChange={(e) => setProfile({ ...profile, bio: e.target.value })}
                      />
                    )}
                  </Field>
                </div>
                <Field label="Category">
                  {(f) => (
                    <select
                      {...f}
                      className="w-full h-12 px-4 rounded-xl border border-line focus-visible:ring-2 focus-visible:ring-brand outline-none bg-surface"
                      value={profile.categoryId || ""}
                      onChange={(e) => setProfile({ ...profile, categoryId: e.target.value })}
                    >
                      <option value="">Select a category</option>
                      {(Array.isArray(categoriesData) ? categoriesData : categoriesData?.results || []).map((cat) => (
                        <option key={cat.id} value={cat.id}>{cat.name}</option>
                      ))}
                    </select>
                  )}
                </Field>
                <Field label="Experience Level">
                  {(f) => (
                    <select
                      {...f}
                      className="w-full h-12 px-4 rounded-xl border border-line focus-visible:ring-2 focus-visible:ring-brand outline-none bg-surface"
                      value={profile.experienceLevel}
                      onChange={(e) => setProfile({ ...profile, experienceLevel: e.target.value })}
                    >
                      <option value="beginner">Beginner</option>
                      <option value="intermediate">Intermediate</option>
                      <option value="expert">Expert</option>
                    </select>
                  )}
                </Field>
                <div className="space-y-1 flex items-end">
                  <label className="flex items-center gap-3 h-12 px-4 rounded-xl border border-line cursor-pointer select-none">
                    <input
                      type="checkbox"
                      checked={profile.isAvailable}
                      onChange={(e) => setProfile({ ...profile, isAvailable: e.target.checked })}
                      className="w-5 h-5 rounded border-line-strong text-brand focus-visible:ring-brand"
                    />
                    <span className="text-sm font-medium text-fg-muted">Available for work</span>
                  </label>
                </div>
              </div>
            </div>
          </section>

          <section className="p-4 md:p-6 bg-surface rounded-2xl border border-line shadow-sm">
  <h2 className="text-lg font-bold mb-4 text-fg">Skills & Expertise</h2>
  
  <div className="flex flex-wrap gap-2 mb-6">
    {profile.skills.length > 0 ? (
      profile.skills.map((skill) => (
        <SkillTag
          key={skill}
          skill={skill}
          onRemove={() => 
            setProfile({ 
              ...profile, 
              skills: profile.skills.filter((s) => s !== skill) 
            })
          }
        />
      ))
    ) : (
      <p className="text-sm text-fg-subtle italic">No skills added yet.</p>
    )}
  </div>

  <div className="flex flex-col sm:flex-row gap-3">
    <div className="flex-1 relative">
      {/* This input had no label element at all -- not a broken one, none --
          so a static scan for unassociated <label>s cannot see it, and it was
          found by rendering the component and checking every emitted control
          for an accessible name. The placeholder is not a substitute: it
          disappears as soon as the user types, and it is not reliably
          announced. The visible "Skills" heading above is consumed as a
          section heading, so the control needs its own name. */}
      <label htmlFor="add-skill" className="sr-only">
        Add a skill
      </label>
      <input
        id="add-skill"
        placeholder="Add skill (e.g. React)..."
        className="w-full h-12 px-4 rounded-xl border border-line bg-surface-2 focus-visible:bg-surface focus-visible:ring-4 focus-visible:ring-fg/5 focus-visible:border-inverse-fg/20 outline-none transition-all text-sm md:text-base"
        value={newSkill}
        onChange={(e) => setNewSkill(e.target.value)}
        onKeyPress={(e) => {
          if (e.key === "Enter" && newSkill.trim()) {
            setProfile({ ...profile, skills: [...profile.skills, newSkill.trim()] });
            setNewSkill("");
          }
        }}
      />
    </div>
    <button
      onClick={() => {
        if (newSkill.trim()) {
          setProfile({ ...profile, skills: [...profile.skills, newSkill.trim()] });
          setNewSkill("");
        }
      }}
      className="w-full sm:w-auto px-8 h-12 bg-inverse text-white rounded-xl font-black uppercase tracking-widest text-xs hover:bg-inverse active:scale-95 transition-all shadow-lg shadow-gray-200"
    >
      Add
    </button>
  </div>
</section>

          <section className="p-6 bg-surface rounded-2xl border border-line shadow-sm">
            <h2 className="text-lg font-bold mb-6 text-fg">Service Packages</h2>
            <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
              <PackageCard title="Starter" data={packages.starter} onChange={(f, v) => handlePackageChange("starter", f, v)} />
              <PackageCard title="Professional" data={packages.pro} onChange={(f, v) => handlePackageChange("pro", f, v)} />
            </div>
          </section>

          <section className="p-6 bg-surface rounded-2xl border border-line shadow-sm">
            <div className="flex items-center justify-between mb-6">
              <h2 className="text-lg font-bold text-fg">Portfolio Items</h2>
              <button 
                onClick={handleOpenAddModal}
                className="flex items-center gap-2 px-4 py-2 bg-brand-soft text-brand rounded-lg font-bold text-sm hover:bg-brand-hover-soft transition-colors"
              >
                <Plus className="w-4 h-4" /> Add Item
              </button>
            </div>

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-6">
              {portfolios.map((item) => (
                <div key={item.id} className="group border border-line-soft rounded-2xl overflow-hidden hover:shadow-md transition-all bg-surface">
                  <div className="aspect-video bg-surface-3 relative">
                    <img src={item.image_url} className="w-full h-full object-cover" alt={item.title} />
                    <div className="absolute inset-0 bg-black/40 opacity-0 group-hover:opacity-100 flex items-center justify-center gap-3 transition-all">
                      <button 
                        onClick={() => handleOpenEditModal(item)}
                        className="p-2 bg-surface rounded-full hover:bg-surface-3 shadow-sm"
                      >
                        <Pencil className="w-4 h-4 text-fg-muted" />
                      </button>
                      <button 
                        onClick={() => handleRemovePortfolio(item.id)}
                        className="p-2 bg-surface rounded-full hover:bg-danger-soft shadow-sm"
                      >
                        <Trash2 className="w-4 h-4 text-danger" />
                      </button>
                    </div>
                  </div>
                  <div className="p-4 border-t border-line-soft">
                    <h3 className="font-bold text-fg truncate">{item.title}</h3>
                    <p className="text-xs text-fg-subtle mt-1 line-clamp-2">{item.description}</p>
                  </div>
                </div>
              ))}
            </div>

            <PortfolioModal 
              isOpen={isModalOpen} 
              onClose={() => setIsModalOpen(false)} 
              onSave={handleSavePortfolioItem}
              initialData={editingItem}
            />
          </section>
        </div>
      </div>
    </div>
  );
}