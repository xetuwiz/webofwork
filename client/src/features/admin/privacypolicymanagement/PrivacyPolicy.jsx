import React, { useState } from "react";
import { ArrowLeft, Pencil, Trash2, Loader2, AlertTriangle, ChevronRight } from "lucide-react";
import Pagination from "../../../components/common/Pagination";
import Dialog from "../../../components/common/Dialog";
import { Field } from "../../../components/common/Field";
import { useListState, toQueryParams } from "../../../hooks/useListState";
import { Link } from "react-router-dom";
import { useForm } from "react-hook-form";
import { usePrivacyMutations } from "./privacyMutations";
import { usePrivacyPolicy } from "./privacyQueries";

export default function PrivacyPolicy() {
  // Page and page size live in the URL, so the view is shareable and a
  // refresh keeps the admin on the section they were editing.
  const { page, pageSize, setPage, setPageSize } = useListState();
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [isDeleteModalOpen, setIsDeleteModalOpen] = useState(false);
  const [selectedSection, setSelectedSection] = useState(null);

  const { register, handleSubmit, reset, setValue, formState: { errors } } = useForm({
    defaultValues: { heading: "", description: "" }
  });

  const { data, isLoading } = usePrivacyPolicy(toQueryParams({}, page, pageSize));
  const { createMutation, updateMutation, deleteMutation } = usePrivacyMutations();

  const handleOpenModal = (section = null) => {
    if (section) {
      setSelectedSection(section);
      setValue("heading", section.heading);
      setValue("description", section.description);
    } else {
      setSelectedSection(null);
      reset({ heading: "", description: "" });
    }
    setIsModalOpen(true);
  };

  const onSubmit = (formData) => {
    if (selectedSection) {
      updateMutation.mutate({ id: selectedSection.id, ...formData }, {
        onSuccess: () => {
          setIsModalOpen(false);
          reset();
        }
      });
    } else {
      createMutation.mutate(formData, {
        onSuccess: () => {
          setIsModalOpen(false);
          reset();
        }
      });
    }
  };

  const confirmDelete = () => {
    deleteMutation.mutate(selectedSection.id, {
      onSuccess: () => setIsDeleteModalOpen(false)
    });
  };

  if (isLoading) return (
    <div className="min-h-screen flex items-center justify-center">
      <Loader2 className="w-8 h-8 animate-spin text-brand" />
    </div>
  );

  return (
    <div className="min-h-screen bg-surface">
      <div className="max-w-page-xl mx-auto px-4 sm:px-8 py-8 md:py-16">
        <Link to="/admin/settings" className="inline-flex items-center gap-2 text-fg-muted hover:text-fg transition-colors mb-8">
          <ArrowLeft className="w-4 h-4" />
          <span className="text-xs font-medium">Back to settings</span>
        </Link>

        <div className="flex flex-col md:flex-row md:items-center md:justify-between gap-4 mb-8">
          <div>
            <h1 className="text-xl sm:text-[26px] font-bold text-fg leading-tight mb-2">Privacy Policy</h1>
            <p className="text-sm text-fg-muted">Update and manage your data protection and privacy clauses.</p>
          </div>
          <button 
            onClick={() => handleOpenModal()}
            className="flex items-center justify-center px-4 py-2.5 bg-brand hover:bg-brand-hover text-brand-fg text-sm font-medium rounded-md transition-colors h-10 w-full sm:w-auto"
          >
            Add Policy Section
          </button>
        </div>

        <div className="border border-line rounded-lg shadow-sm overflow-hidden">
          <div className="overflow-x-auto">
            <div className="min-w-[800px] md:min-w-full">
              <div className="grid grid-cols-[80px_200px_1fr_120px] bg-surface-2 border-b border-line">
                <div className="px-6 py-3 text-[10px] font-bold text-fg-subtle uppercase tracking-wider"># ID</div>
                <div className="px-6 py-3 text-[10px] font-bold text-fg-subtle uppercase tracking-wider">Policy Heading</div>
                <div className="px-6 py-3 text-[10px] font-bold text-fg-subtle uppercase tracking-wider">Content Preview</div>
                <div className="px-6 py-3 text-[10px] font-bold text-fg-subtle uppercase tracking-wider text-right">Actions</div>
              </div>

              {data?.results?.map((section, index) => (
                <div key={section.id} className={`grid grid-cols-[80px_200px_1fr_120px] border-b border-line last:border-b-0 ${index % 2 !== 0 ? "bg-surface-2" : "bg-surface"}`}>
                  <div className="px-6 py-6 flex items-center text-xs text-fg-subtle font-mono">#{index+1}</div>
                  <div className="px-6 py-6 flex items-center min-w-0 overflow-hidden">
                    <div className="text-xs font-bold text-fg break-words line-clamp-6">
                      {section.heading}
                    </div>
                  </div>
                  <div className="px-6 py-6 flex items-center min-w-0">
                    <p className="text-xs text-fg-muted line-clamp-25 leading-relaxed break-words">{section.description}</p>
                  </div>
                  <div className="px-6 py-6 flex items-center justify-end gap-2 shrink-0">
                    <button onClick={() => handleOpenModal(section)} className="p-2 hover:bg-surface rounded-lg border border-transparent hover:border-line transition-all">
                      <Pencil className="w-4 h-4 text-fg-muted" />
                    </button>
                    <button 
                      onClick={() => { setSelectedSection(section); setIsDeleteModalOpen(true); }}
                      className="p-2 hover:bg-danger-soft rounded-lg border border-transparent hover:border-danger/30 transition-all"
                    >
                      <Trash2 className="w-4 h-4 text-danger-soft-fg" />
                    </button>
                  </div>
                </div>
              ))}
            </div>
          </div>

          <div className="px-6 py-4 border-t border-line-soft bg-surface">
            {/* Replaces a hand-rolled prev/next bar whose counter read
                `results.length` -- this page's row count, not the range
                start -- so on page 3 it said "Showing 10 of 42". */}
            <Pagination
              page={page}
              pageSize={pageSize}
              count={data?.count ?? 0}
              onPageChange={setPage}
              onPageSizeChange={setPageSize}
              itemNoun="sections"
              className="pt-0"
            />
          </div>
        </div>
      </div>

      {/* Both were hand-rolled `fixed inset-0 z-50` scrims: no focus trap, no Esc
          to close, no scroll lock, no `aria-modal` -- so the page behind stayed
          keyboard-reachable while a dialog was open. The two form labels also had
          no `htmlFor` and no matching id, so they announced as bare "edit text". */}
      <Dialog
        open={isModalOpen}
        onClose={() => setIsModalOpen(false)}
        title={selectedSection ? "Edit Privacy Section" : "Add New Privacy Section"}
        size="lg"
      >
        <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
          <Field label="Section Heading" error={errors.heading?.message}>
            {(f) => (
              <input
                type="text"
                {...register("heading", { required: "Heading is required", maxLength: { value: 255, message: "Max length 255 chars" } })}
                {...f}
                className={`w-full px-4 py-2.5 bg-surface-2 border rounded-lg text-sm focus-visible:ring-2 focus-visible:ring-brand outline-none`}
                placeholder="e.g. Data Collection"
              />
            )}
          </Field>
          <Field
            label="Detailed Description"
            error={errors.description?.message}
            hint="This text is shown to users on the public privacy page."
          >
            {(f) => (
              <textarea
                rows={8}
                {...register("description", { required: "Description is required" })}
                {...f}
                className={`w-full px-4 py-2.5 bg-surface-2 border rounded-lg text-sm focus-visible:ring-2 focus-visible:ring-brand outline-none resize-none`}
                placeholder="Provide full details of this privacy policy section..."
              />
            )}
          </Field>
          <div className="pt-2">
            <button type="submit" className="w-full py-3 bg-brand text-brand-fg rounded-lg text-xs font-bold uppercase tracking-widest hover:bg-brand-hover transition-colors">
              {createMutation.isPending || updateMutation.isPending ? "Updating..." : "Save Policy Changes"}
            </button>
          </div>
        </form>
      </Dialog>

      <Dialog
        open={isDeleteModalOpen}
        onClose={() => setIsDeleteModalOpen(false)}
        title="Remove Section?"
        description={`You are about to delete "${selectedSection?.heading}". This may affect legal transparency for your users.`}
        size="sm"
        footer={
          <div className="flex flex-col sm:flex-row gap-3">
            <button
              type="button"
              onClick={() => setIsDeleteModalOpen(false)}
              className="order-2 sm:order-1 flex-1 py-2.5 border rounded-lg text-xs font-bold uppercase"
            >
              Cancel
            </button>
            <button
              type="button"
              onClick={confirmDelete}
              className="order-1 sm:order-2 flex-1 py-2.5 bg-danger text-danger-fg rounded-lg text-xs font-bold uppercase hover:bg-danger"
            >
              {deleteMutation.isPending ? "Removing..." : "Delete Permanently"}
            </button>
          </div>
        }
      >
        <div className="w-12 h-12 bg-danger-soft rounded-full flex items-center justify-center mx-auto">
          <AlertTriangle className="w-6 h-6 text-danger-soft-fg" aria-hidden="true" />
        </div>
      </Dialog>
    </div>
  );
}
