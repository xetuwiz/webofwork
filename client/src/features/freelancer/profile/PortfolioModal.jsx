import React, { useState, useEffect } from "react";
import { Camera, X } from "lucide-react";
import { uploadToCloudinary } from "../../../utils/cloudinaryHelper";
import Dialog from "../../../components/common/Dialog";
import { Field } from "../../../components/common/Field";

export default function PortfolioModal({ isOpen, onClose, onSave, initialData }) {
  const [item, setItem] = useState({ title: "", description: "", image_url: "" });
  const [uploading, setUploading] = useState(false);

  useEffect(() => {
    if (initialData) {
      setItem(initialData);
    } else {
      setItem({ title: "", description: "", image_url: "" });
    }
  }, [initialData, isOpen]);

  const handleImageUpload = async (e) => {
    const file = e.target.files[0];
    if (!file) return;
    setUploading(true);
    try {
      const data = await uploadToCloudinary(file);
      setItem((prev) => ({ ...prev, image_url: data.secure_url }));
    } catch (err) {
      alert("Upload failed");
    } finally {
      setUploading(false);
    }
  };

  if (!isOpen) return null;

  return (
    <Dialog
      open={isOpen}
      onClose={onClose}
      title={initialData ? "Edit Project" : "Add New Project"}
      size="md"
      className="overflow-hidden"
    >
        <div className="flex items-center justify-between p-6 border-b">
          <h3 className="text-xl font-bold text-fg">
            {initialData ? "Edit Project" : "Add New Project"}
          </h3>
          <button onClick={onClose} className="p-1 hover:bg-surface-3 rounded-full">
            <X className="w-5 h-5 text-fg-subtle" />
          </button>
        </div>

        <div className="p-6 space-y-5">
          <div className="space-y-2">
            {/* A group, not a single control: this heading sits over a dropzone
                whose own <label> already names the file input. Two nested
                labels could never both be right, so the outer one becomes a
                legend. The inner wrapping label is left exactly as it was. */}
            <fieldset>
              <legend className="text-xs font-bold text-fg-subtle uppercase mb-2">Project Image</legend>
              <div className="relative aspect-video bg-surface-2 rounded-xl border-2 border-dashed border-line overflow-hidden group">
                {item.image_url ? (
                  <img src={item.image_url} className="w-full h-full object-cover" alt="Preview" />
                ) : (
                  <div className="flex flex-col items-center justify-center h-full text-fg-subtle" aria-hidden="true">
                    <Camera size={32} strokeWidth={1.5} />
                    <span className="text-xs mt-2 font-medium">Click to upload work</span>
                  </div>
                )}

                <label className="absolute inset-0 cursor-pointer flex items-center justify-center bg-black/40 opacity-0 group-hover:opacity-100 transition-opacity">
                  <span className="bg-surface px-4 py-2 rounded-lg text-sm font-bold shadow-lg" aria-hidden="true">
                    {uploading ? "Uploading..." : "Browse Image"}
                  </span>
                  <input type="file" className="hidden" onChange={handleImageUpload} accept="image/*" />
                </label>
              </div>
            </fieldset>
          </div>

          <div className="space-y-1">
            <Field label="Title">
              {(field) => (
                <input
                  placeholder="e.g. E-commerce Dashboard"
                  {...field}
                  className="w-full h-12 px-4 rounded-xl border border-line focus-visible:ring-2 focus-visible:ring-brand outline-none transition-all"
                  value={item.title}
                  onChange={(e) => setItem({ ...item, title: e.target.value })}
                />
              )}
            </Field>
          </div>

          <div className="space-y-1">
            <Field label="Description">
              {(field) => (
                <textarea
                  placeholder="Briefly describe your role and the technologies used..."
                  rows={3}
                  {...field}
                  className="w-full p-4 rounded-xl border border-line focus-visible:ring-2 focus-visible:ring-brand outline-none resize-none transition-all"
                  value={item.description}
                  onChange={(e) => setItem({ ...item, description: e.target.value })}
                />
              )}
            </Field>
          </div>
        </div>

        <div className="p-6 bg-surface-2 flex gap-3">
          <button
            onClick={onClose}
            className="flex-1 h-12 rounded-xl font-bold text-fg-muted hover:bg-surface-inset transition-colors"
          >
            Cancel
          </button>
          <button
            disabled={!item.image_url || !item.title || uploading}
            onClick={() => onSave(item)}
            className="flex-2 h-12 bg-brand text-brand-fg rounded-xl font-bold shadow-lg shadow-blue-200 hover:bg-brand-hover disabled:opacity-50 transition-all"
          >
            {initialData ? "Update Project" : "Add to Portfolio"}
          </button>
        </div>
    </Dialog>
  );
}
