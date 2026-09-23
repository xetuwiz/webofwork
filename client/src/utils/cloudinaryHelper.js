export async function uploadToCloudinary(file) {
  if (!(file instanceof File)) throw new TypeError("A file is required for upload");

  const cloudName = import.meta.env.VITE_CLOUDINARY_CLOUD_NAME;
  const uploadPreset = import.meta.env.VITE_CLOUDINARY_UPLOAD_PRESET;
  if (!cloudName || !uploadPreset) throw new Error("Cloudinary upload configuration is missing");

  const formData = new FormData();
  formData.append("file", file);
  formData.append("upload_preset", uploadPreset);

  const response = await fetch(`https://api.cloudinary.com/v1_1/${cloudName}/auto/upload`, { method: "POST", body: formData });
  if (!response.ok) throw new Error(`Cloudinary upload failed with status ${response.status}`);
  return response.json();
}