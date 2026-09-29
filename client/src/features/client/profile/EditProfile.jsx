import { useRef, useState } from "react";
import { Camera, MapPin, Loader2 } from "lucide-react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useSelector } from "react-redux";
import { profileSchema } from "./profileSchema";
import { uploadToCloudinary } from "../../../utils/cloudinaryHelper";
import { useUpdateProfile } from "./profileMutations";
import { useNavigate } from "react-router-dom";
import { useProfile } from "./profileQueries";
import { toastSuccess } from "../../../utils/toast";
import Button from "../../../components/common/Button";
import { Field } from "../../../components/common/Field";

export default function ProfileEdit() {
  const { user } = useSelector((state) => state.auth);
  const {data:profile,isLoading} = useProfile()
  const navigate = useNavigate();
  const fileInputRef = useRef(null);
  const [previewUrl, setPreviewUrl] = useState(user?.profile_photo || null);
  const [isUploading, setIsUploading] = useState(false);

  const { mutateAsync: updateProfile } = useUpdateProfile();

  const {
    register,
    handleSubmit,
    setValue,
    formState: { errors, isDirty },
  } = useForm({
    resolver: zodResolver(profileSchema),
    values: {
      fullName: profile?.full_name || "",
      companyName: profile?.company_name || "",
      location: profile?.location || "",
    },
  });

  const handleImageChange = (e) => {
    const file = e.target.files?.[0];
    if (!file) return;

    setValue("profileImage", file, { shouldDirty: true });

    const reader = new FileReader();
    reader.onloadend = () => setPreviewUrl(reader.result);
    reader.readAsDataURL(file);
  };

  const onSubmit = async (data) => {
    try {
      setIsUploading(true);

      let profilePhotoUrl = user?.profile_photo;

      if (data.profileImage) {
        const uploadRes = await uploadToCloudinary(data.profileImage);
        profilePhotoUrl = uploadRes.secure_url;
      }

      await updateProfile({
        full_name: data.fullName,
        company_name: data.companyName,
        location: data.location,
        profile_photo: profilePhotoUrl,
      });
      navigate("/profile")
      toastSuccess("profile updated")
    } finally {
      setIsUploading(false);
    }
  };
if (isLoading) return <Loader2 className="animate-spin" />;
  return (
    <div className="min-h-screen bg-surface-2 py-8 px-4 sm:px-6 lg:px-8">
      <form onSubmit={handleSubmit(onSubmit)} className="max-w-3xl mx-auto space-y-6">
        <h1 className="text-2xl font-bold text-fg">Edit Profile</h1>

        <section className="bg-surface p-6 rounded-xl border border-line shadow-sm">
          <h2 className="text-sm font-semibold text-fg-muted uppercase tracking-wider mb-4">
            Profile Photo
          </h2>

          <div className="flex items-center gap-6">
            <div
              className="relative group cursor-pointer"
              onClick={() => fileInputRef.current.click()}
            >
              <div className="w-24 h-24 rounded-full bg-brand flex items-center justify-center text-brand-fg text-2xl font-bold overflow-hidden border-4 border-white shadow-md">
                {previewUrl ? (
                  <img
                    src={previewUrl}
                    alt="Preview"
                    className="w-full h-full object-cover"
                  />
                ) : (
                  user?.full_name?.[0].toUpperCase() || "UN"
                )}
              </div>

              <div className="absolute inset-0 bg-black/20 rounded-full flex items-center justify-center opacity-0 group-hover:opacity-100 transition-opacity">
                <Camera className="text-white w-6 h-6" />
              </div>

              <input
                ref={fileInputRef}
                type="file"
                accept="image/*"
                className="hidden"
                onChange={handleImageChange}
              />
            </div>

            <div>
              <button
                type="button"
                onClick={() => fileInputRef.current.click()}
                className="px-4 py-2 border border-line-strong rounded-lg text-sm font-medium hover:bg-surface-2"
              >
                Change Photo
              </button>
              <p className="text-xs text-fg-subtle mt-2">
                JPG, PNG or WEBP. Max 5MB.
              </p>
            </div>
          </div>
        </section>

        <section className="bg-surface p-6 rounded-xl border border-line shadow-sm space-y-4">
          <h2 className="text-sm font-semibold text-fg-muted uppercase tracking-wider">
            Basic Information
          </h2>

          <div className="grid grid-cols-1 gap-4">
            <Field label="Full Name" required error={errors.fullName?.message}>
              {(f) => (
                <input
                  {...register("fullName")}
                  {...f}
                  className={`w-full p-2.5 border rounded-lg outline-none focus-visible:ring-2 ${errors.fullName
                      ? "border-red-500 focus-visible:ring-danger/20"
                      : "border-line-strong focus-visible:ring-brand"
                    }`}
                />
              )}
            </Field>

            {/* "(Optional)" was baked into the label text, so it was read as
                part of the field's name. It is now a `hint`, which is what it
                is -- supplementary information rather than the name. */}
            <Field label="Company Name" hint="Optional">
              {(f) => (
                <input
                  {...register("companyName")}
                  {...f}
                  className="w-full p-2.5 border border-line-strong rounded-lg outline-none focus-visible:ring-2 focus-visible:ring-brand"
                />
              )}
            </Field>

            <Field label="Location" hint="Optional">
              {(f) => (
                <div className="relative">
                  <MapPin className="absolute left-3 top-1/2 -translate-y-1/2 text-fg-subtle w-4 h-4" aria-hidden="true" />
                  <input
                    {...register("location")}
                    {...f}
                    className="w-full pl-10 pr-4 py-2.5 border border-line-strong rounded-lg outline-none focus-visible:ring-2 focus-visible:ring-brand"
                  />
                </div>
              )}
            </Field>
          </div>
        </section>

        <div className="flex items-center gap-4 pt-4">
          <Button
            type="submit"
            variant="primary"
            size="none"
            loading={isUploading}
            disabled={isUploading || !isDirty}
            className="px-6 py-2.5 font-medium disabled:bg-brand"
          >
            Save Changes
          </Button>

          <button
            type="button"
            onClick={() => navigate("/profile")}
            className="px-6 py-2.5 border border-line-strong rounded-lg font-medium hover:bg-surface-2"
          >
            Cancel
          </button>
        </div>
      </form>
    </div>
  );
}
